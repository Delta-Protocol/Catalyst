
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
using System.Linq;
using System.Threading.Tasks;
using Catalyst.Common.Config;
using Catalyst.Common.Extensions;
using Catalyst.Common.Interfaces.FileTransfer;
using Catalyst.Common.Interfaces.IO.Messaging.Dto;
using Catalyst.Common.IO.Messaging;
using Catalyst.Common.Util;
using Catalyst.Node.Core.RPC.Observables;
using Catalyst.Protocol.Common;
using Catalyst.Protocol.Rpc.Node;
using Catalyst.TestUtils;
using DotNetty.Transport.Channels;
using FluentAssertions;
using Google.Protobuf;
using NSubstitute;
using Serilog;
using Xunit;

namespace Catalyst.Node.Core.UnitTests.RPC.Observables
{
    public sealed class TransferFileBytesRequestObserverTest
    {
        private readonly TransferFileBytesRequestObserver _observer;
        private readonly IDownloadFileTransferFactory _downloadFileTransferFactory;
        private readonly IChannelHandlerContext _context;

        public TransferFileBytesRequestObserverTest()
        {
            _context = Substitute.For<IChannelHandlerContext>();
            _context.Channel.Returns(Substitute.For<IChannel>());
            _downloadFileTransferFactory = Substitute.For<IDownloadFileTransferFactory>();
            var peerIdentifier = PeerIdentifierHelper.GetPeerIdentifier("Test");
            _observer = new TransferFileBytesRequestObserver(_downloadFileTransferFactory,
                peerIdentifier,
                Substitute.For<ILogger>());
        }

        [Fact]
        public void CanHandlerDownloadChunk()
        {
            var guid = Guid.NewGuid();
            var request = new TransferFileBytesRequest
            {
                ChunkBytes = ByteString.Empty,
                ChunkId = 1,
                CorrelationFileName = Guid.NewGuid().ToByteString()
            }.ToProtocolMessage(PeerIdHelper.GetPeerId("Test"), guid);

            _downloadFileTransferFactory.DownloadChunk(Arg.Any<Guid>(), Arg.Any<uint>(), Arg.Any<byte[]>())
               .Returns(FileTransferResponseCodes.Successful);

            request.SendToHandler(_context, _observer);
            _downloadFileTransferFactory.Received(1).DownloadChunk(Arg.Any<Guid>(), Arg.Any<uint>(), Arg.Any<byte[]>());
        }

        [Fact]
        public async Task HandlerCanSendErrorOnException()
        {
            var guid = Guid.NewGuid();
            var request = new TransferFileBytesRequest
            {
                ChunkBytes = ByteString.Empty,
                ChunkId = 1,
                CorrelationFileName = ByteString.Empty
            }.ToProtocolMessage(PeerIdHelper.GetPeerId("Test"), guid);
            
            var messageStream = MessageStreamHelper.CreateStreamWithMessage(_context, request);
            
            _observer.StartObserving(messageStream);

            await messageStream.WaitForEndOfDelayedStreamOnTaskPoolScheduler();

            var receivedCalls = _context.Channel.ReceivedCalls().ToList();
            receivedCalls.Count.Should().Be(1);
            var sentResponseDto = (IMessageDto) receivedCalls.Single().GetArguments().Single();
            sentResponseDto.Message.Descriptor.ShortenedFullName().Should().Be(TransferFileBytesResponse.Descriptor.ShortenedFullName());
            var versionResponseMessage = sentResponseDto.FromIMessageDto<TransferFileBytesResponse>();
            versionResponseMessage.ResponseCode.Should().Equal((byte) FileTransferResponseCodes.Error);
        }
    }
}