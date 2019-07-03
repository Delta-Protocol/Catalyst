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
using Catalyst.Common.Interfaces.Cli;
using Catalyst.Common.Interfaces.IO.Messaging;
using Catalyst.Common.Interfaces.IO.Observers;
using Catalyst.Common.Interfaces.P2P;
using Catalyst.Common.IO.Observers;
using Catalyst.Protocol.Rpc.Node;
using Dawn;
using DotNetty.Transport.Channels;
using ILogger = Serilog.ILogger;

namespace Catalyst.Node.Rpc.Client.IO.Observers
{
    /// <summary>
    /// Handles the Peer reputation response
    /// </summary>
    /// <seealso cref="IRpcResponseObserver" />
    public sealed class PeerBlackListingResponseObserver
        : ResponseObserverBase<SetPeerBlackListResponse>,
            IRpcResponseObserver
    {
        private readonly IUserOutput _output;

        /// <summary>
        /// Initializes a new instance of the <see cref="PeerBlackListingResponseObserver"/> class.
        /// </summary>
        /// <param name="output">The output.</param>
        /// <param name="logger">The logger.</param>
        public PeerBlackListingResponseObserver(IUserOutput output,
            ILogger logger)
            : base(logger)
        {
            _output = output;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="setPeerBlackListResponse"></param>
        /// <param name="channelHandlerContext"></param>
        /// <param name="senderPeerIdentifier"></param>
        /// <param name="correlationId"></param>
        protected override void HandleResponse(SetPeerBlackListResponse setPeerBlackListResponse,
            IChannelHandlerContext channelHandlerContext,
            IPeerIdentifier senderPeerIdentifier,
            ICorrelationId correlationId)
        {
            Guard.Argument(setPeerBlackListResponse, nameof(setPeerBlackListResponse)).NotNull();
            Guard.Argument(channelHandlerContext, nameof(channelHandlerContext)).NotNull();
            Guard.Argument(senderPeerIdentifier, nameof(senderPeerIdentifier)).NotNull();
            Logger.Debug("Handling GetPeerBlackList response");

            try
            {
                var msg = setPeerBlackListResponse.PublicKey.ToStringUtf8() == string.Empty
                    ? "Peer not found"
                    : $"Peer Blacklisting Successful : " +
                    $"{setPeerBlackListResponse.Blacklist.ToString()}, " +
                    $"{setPeerBlackListResponse.PublicKey.ToStringUtf8()}, " +
                    $"{setPeerBlackListResponse.Ip.ToStringUtf8()}";
                   
                _output.WriteLine(msg);
            }
            catch (Exception ex)
            {
                Logger.Error(ex,
                    "Failed to handle GetPeerBlackListingResponse after receiving message {0}", setPeerBlackListResponse);
                throw;
            }
        }
    }
}
