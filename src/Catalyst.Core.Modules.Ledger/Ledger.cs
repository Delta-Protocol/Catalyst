#region LICENSE

/**
* Copyright (c) 2019 Catalyst Network
*
* This file is part of Catalyst.Node <https://github.com/catalyst-network/Catalyst.Node>
*
* Catalyst.Node is free software: you can redistribute it and/or modify
* it under the terms of the GNU General Public License as published by
* the Free Software Foundation, either version 2 of the License, or
* (at your option) any later version.
*
* Catalyst.Node is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU General Public License for more details.
*
* You should have received a copy of the GNU General Public License
* along with Catalyst.Node. If not, see <https://www.gnu.org/licenses/>.
*/

#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autofac.Features.AttributeFilters;
using Catalyst.Abstractions.Consensus.Deltas;
using Catalyst.Abstractions.Hashing;
using Catalyst.Abstractions.Kvm;
using Catalyst.Abstractions.Ledger;
using Catalyst.Abstractions.Ledger.Models;
using Catalyst.Abstractions.Mempool;
using Catalyst.Abstractions.Repository;
using Catalyst.Core.Lib.DAO;
using Catalyst.Core.Lib.DAO.Transaction;
using Catalyst.Core.Lib.Extensions;
using Catalyst.Core.Modules.Kvm;
using Catalyst.Core.Modules.Ledger.Repository;
using Catalyst.Protocol.Deltas;
using Catalyst.Protocol.Transaction;
using Dawn;
using Google.Protobuf;
using LibP2P;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Dirichlet.Numerics;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Store;
using Serilog;
using Serilog.Events;
using TheDotNetLeague.MultiFormats.MultiHash;
using Account = Catalyst.Abstractions.Ledger.Models.Account;

namespace Catalyst.Core.Modules.Ledger
{
    /// <summary>
    ///     This class represents a ledger and is a collection of accounts and data store.
    /// </summary>
    /// <inheritdoc cref="ILedger" />
    /// <inheritdoc cref="IDisposable" />
    public sealed class Ledger : ILedger, IDisposable
    {
        public IAccountRepository Accounts { get; }
        private readonly IDeltaExecutor _deltaExecutor;
        private readonly IStateProvider _stateProvider;
        private readonly IStorageProvider _storageProvider;
        private readonly ISnapshotableDb _stateDb;
        private readonly IDb _codeDb;
        private readonly IDeltaByNumberRepository _deltas;
        private readonly ITransactionRepository _receipts;
        private readonly ILedgerSynchroniser _synchroniser;
        private readonly IMempool<PublicEntryDao> _mempool;
        private readonly IMapperProvider _mapperProvider;
        private readonly IHashProvider _hashProvider;
        private readonly ILogger _logger;
        private readonly IDisposable _deltaUpdatesSubscription;

        private readonly object _synchronisationLock = new object();
        volatile Cid _latestKnownDelta;
        long _latestKnownDeltaNumber = -1;

        public Cid LatestKnownDelta => _latestKnownDelta;

        public long LatestKnownDeltaNumber => Volatile.Read(ref _latestKnownDeltaNumber);

        public bool IsSynchonising => Monitor.IsEntered(_synchronisationLock);

        public Ledger(IDeltaExecutor deltaExecutor,
            IStateProvider stateProvider,
            IStorageProvider storageProvider,
            ISnapshotableDb stateDb,
            IDb codeDb,
            IAccountRepository accounts,
            IDeltaByNumberRepository deltas,
            ITransactionRepository receipts,
            IDeltaHashProvider deltaHashProvider,
            ILedgerSynchroniser synchroniser,
            IMempool<PublicEntryDao> mempool,
            IMapperProvider mapperProvider,
            IHashProvider hashProvider,
            ILogger logger)
        {
            Accounts = accounts;
            _deltaExecutor = deltaExecutor;
            _stateProvider = stateProvider;
            _storageProvider = storageProvider;
            _stateDb = stateDb;
            _codeDb = codeDb;
            _deltas = deltas;
            _synchroniser = synchroniser;
            _mempool = mempool;
            _mapperProvider = mapperProvider;
            _hashProvider = hashProvider;
            _logger = logger;
            _receipts = receipts;

            _deltaUpdatesSubscription = deltaHashProvider.DeltaHashUpdates.Subscribe(Update);
            WriteLatestKnownDelta(_synchroniser.DeltaCache.GenesisHash);
        }

        private void FlushTransactionsFromDelta(Cid deltaHash)
        {
            _synchroniser.DeltaCache.TryGetOrAddConfirmedDelta(deltaHash, out var delta);
            if (delta != null)
            {
                var deltaTransactions = delta.PublicEntries.Select(x => x.ToDao<PublicEntry, PublicEntryDao>(_mapperProvider));
                _mempool.Service.Delete(deltaTransactions);
            }
        }

        /// <inheritdoc />
        public bool SaveAccountState(Account account)
        {
            Guard.Argument(account, nameof(account)).NotNull();

            try
            {
                Accounts.Add(account);
                return true;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Failed to add account state to the Ledger");
                return false;
            }
        }

        /// <inheritdoc />
        public void Update(Cid deltaHash)
        {
            try
            {
                lock (_synchronisationLock)
                {
                    var chainedDeltaHashes = _synchroniser
                       .CacheDeltasBetween(LatestKnownDelta, deltaHash, CancellationToken.None)
                       .Reverse()
                       .ToList();

                    if (!Equals(chainedDeltaHashes.First(), LatestKnownDelta))
                    {
                        _logger.Warning(
                            "Failed to walk back the delta chain to {LatestKnownDelta}, giving up ledger update.",
                            LatestKnownDelta);
                        return;
                    }

                    foreach (var chainedDeltaHash in chainedDeltaHashes.Skip(1))
                    {
                        UpdateLedgerFromDelta(chainedDeltaHash);
                    }
                }

                FlushTransactionsFromDelta(deltaHash);
            }
            catch (Exception exception)
            {
                _logger.Error(exception, "Failed to update the ledger using the delta with hash {deltaHash}",
                    deltaHash);
            }
        }

        private void UpdateLedgerFromDelta(Cid deltaHash)
        {
            var stateSnapshot = _stateDb.TakeSnapshot();
            if (stateSnapshot != -1)
            {
                if (_logger.IsEnabled(LogEventLevel.Error))
                {
                    _logger.Error("Uncommitted state ({stateSnapshot}) when processing from a branch root {branchStateRoot} starting with delta {deltaHash}",
                        stateSnapshot,
                        null,
                        deltaHash);
                }
            }
            
            var snapshotStateRoot = _stateProvider.StateRoot;

            try
            {
                if (!_synchroniser.DeltaCache.TryGetOrAddConfirmedDelta(deltaHash, out Delta nextDeltaInChain))
                {
                    _logger.Warning("Failed to retrieve Delta with hash {hash} from the Dfs, ledger has not been updated.", deltaHash);
                    return;
                }
                
                if (!_synchroniser.DeltaCache.TryGetOrAddConfirmedDelta(Cid.Read(nextDeltaInChain.PreviousDeltaDfsHash.ToByteArray()), out Delta parentDelta))
                {
                    _logger.Warning("Failed to retrieve parent Delta with hash {hash} from the Dfs, ledger has not been updated.", deltaHash);
                    return;
                }

                ReceiptDeltaTracer tracer = new ReceiptDeltaTracer(nextDeltaInChain, deltaHash);

                // add here a receipts tracer or similar, depending on what data needs to be stored for each contract
                _stateProvider.Reset();
                _storageProvider.Reset();
                
                _stateProvider.StateRoot = new Keccak(parentDelta.StateRoot?.ToByteArray());
                _deltaExecutor.Execute(nextDeltaInChain, tracer);

                // store receipts
                if (tracer.Receipts.Any())
                {
                    _receipts.Put(deltaHash, tracer.Receipts.ToArray(), nextDeltaInChain.PublicEntries.ToArray());
                }
                
                _stateDb.Commit();

                // this should be set in the builder

                WriteLatestKnownDelta(deltaHash);
            }
            catch
            {
                Restore(stateSnapshot, snapshotStateRoot);
            }
        }

        void WriteLatestKnownDelta(Cid deltaHash)
        {
            _latestKnownDelta = deltaHash;

            Volatile.Write(ref _latestKnownDeltaNumber, _latestKnownDeltaNumber + 1);
            _deltas.Map(_latestKnownDeltaNumber, deltaHash); // store delta numbers
        }

        private void Restore(int stateSnapshot, Keccak snapshotStateRoot)
        {
            if (_logger.IsEnabled(LogEventLevel.Verbose))
            {
                _logger.Verbose("Reverting deltas {stateRoot}", _stateProvider.StateRoot);
            }

            _stateDb.Restore(stateSnapshot);
            _storageProvider.Reset();
            _stateProvider.Reset();
            _stateProvider.StateRoot = snapshotStateRoot;
            if (_logger.IsEnabled(LogEventLevel.Verbose))
            {
                _logger.Verbose("Reverted deltas {stateRoot}", _stateProvider.StateRoot);
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            Accounts?.Dispose();
            _deltaUpdatesSubscription?.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        sealed class ReceiptDeltaTracer : ITxTracer
        {
            readonly Delta _delta;
            readonly long _deltaNumber;
            readonly Cid _deltaHash;
            readonly List<TransactionReceipt> _txReceipts;
            int _currentIndex;

            public ReceiptDeltaTracer(Delta delta, Cid deltaHash)
            {
                _delta = delta;
                _deltaHash = deltaHash;
                _deltaNumber = delta.DeltaNumber;
                _txReceipts = new List<TransactionReceipt>(delta.PublicEntries.Count);
            }

            public IEnumerable<TransactionReceipt> Receipts => _txReceipts;

            public bool IsTracingReceipt => true;

            public void MarkAsSuccess(Address recipient, long gasSpent, byte[] output, LogEntry[] logs)
            {
                _txReceipts.Add(BuildReceipt(recipient, gasSpent, StatusCode.Success, logs));
            }

            public void MarkAsFailed(Address recipient, long gasSpent, byte[] output, string error)
            {
                _txReceipts.Add(BuildFailedReceipt(recipient, gasSpent));
            }

            private TransactionReceipt BuildFailedReceipt(Address recipient, long gasSpent)
            {
                return BuildReceipt(recipient, gasSpent, StatusCode.Failure, LogEntry.EmptyLogs);
            }

            private TransactionReceipt BuildReceipt(Address recipient, long spentGas, byte statusCode, LogEntry[] logEntries)
            {
                PublicEntry entry = _delta.PublicEntries[_currentIndex];

                TransactionReceipt txReceipt = new TransactionReceipt
                {
                    Logs = logEntries,
                    GasUsedTotal = _delta.GasUsed,
                    StatusCode = statusCode,
                    Recipient = entry.IsContractDeployment ? null : recipient,
                    DeltaHash = _deltaHash,
                    DeltaNumber = _deltaNumber,
                    Index = _currentIndex,
                    GasUsed = spentGas,
                    Sender = GetAccountAddress(entry.SenderAddress),
                    ContractAddress = entry.IsContractDeployment ? recipient : null,
                };

                _currentIndex += 1;

                return txReceipt;
            }

            private static Address GetAccountAddress(ByteString publicKeyByteString)
            {
                if (publicKeyByteString == null || publicKeyByteString.IsEmpty)
                {
                    return null;
                }

                return publicKeyByteString.ToByteArray().ToKvmAddress();
            }

            public bool IsTracingActions => false;
            public bool IsTracingOpLevelStorage => false;
            public bool IsTracingMemory => false;
            public bool IsTracingInstructions => false;
            public bool IsTracingCode => false;
            public bool IsTracingStack => false;
            public bool IsTracingState => false;
            public void ReportBalanceChange(Address address, UInt256? before, UInt256? after) { throw new NotImplementedException(); }
            public void ReportCodeChange(Address address, byte[] before, byte[] after) { throw new NotImplementedException(); }
            public void ReportNonceChange(Address address, UInt256? before, UInt256? after) { throw new NotImplementedException(); }
            public void ReportStorageChange(StorageAddress storageAddress, byte[] before, byte[] after) { throw new NotImplementedException(); }
            public void StartOperation(int depth, long gas, Instruction opcode, int pc) { throw new NotImplementedException(); }
            public void ReportOperationError(EvmExceptionType error) { throw new NotImplementedException(); }
            public void ReportOperationRemainingGas(long gas) { throw new NotImplementedException(); }
            public void SetOperationStack(List<string> stackTrace) { throw new NotImplementedException(); }
            public void ReportStackPush(Span<byte> stackItem) { throw new NotImplementedException(); }
            public void SetOperationMemory(List<string> memoryTrace) { throw new NotImplementedException(); }
            public void SetOperationMemorySize(ulong newSize) { throw new NotImplementedException(); }
            public void ReportMemoryChange(long offset, Span<byte> data) { throw new NotImplementedException(); }
            public void ReportStorageChange(Span<byte> key, Span<byte> value) { throw new NotImplementedException(); }
            public void SetOperationStorage(Address address, UInt256 storageIndex, byte[] newValue, byte[] currentValue) { throw new NotImplementedException(); }
            public void ReportSelfDestruct(Address address, UInt256 balance, Address refundAddress) { throw new NotImplementedException(); }

            public void ReportAction(long gas,
                UInt256 value,
                Address @from,
                Address to,
                byte[] input,
                ExecutionType callType,
                bool isPrecompileCall = false)
            {
                throw new NotImplementedException();
            }

            public void ReportActionEnd(long gas, byte[] output) { throw new NotImplementedException(); }
            public void ReportActionError(EvmExceptionType evmExceptionType) { throw new NotImplementedException(); }
            public void ReportActionEnd(long gas, Address deploymentAddress, byte[] deployedCode) { throw new NotImplementedException(); }
            public void ReportByteCode(byte[] byteCode) { throw new NotImplementedException(); }
            public void ReportRefund(long gasAvailable) { throw new NotImplementedException(); }
        }
    }
}
