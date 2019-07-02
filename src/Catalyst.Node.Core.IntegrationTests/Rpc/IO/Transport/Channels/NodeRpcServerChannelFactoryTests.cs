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

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Catalyst.Common.Extensions;
using Catalyst.Common.Interfaces.IO.Messaging;
using Catalyst.Common.Interfaces.Modules.KeySigner;
using Catalyst.Common.Interfaces.P2P;
using Catalyst.Common.Interfaces.Rpc.Authentication;
using Catalyst.Common.IO.Handlers;
using Catalyst.Common.IO.Messaging;
using Catalyst.Common.IO.Messaging.Dto;
using Catalyst.Common.Util;
using Catalyst.Cryptography.BulletProofs.Wrapper.Types;
using Catalyst.Node.Rpc.Client.UnitTests.IO.Transport.Channels;
using Catalyst.Protocol.Common;
using Catalyst.Protocol.Rpc.Node;
using Catalyst.TestUtils;
using DotNetty.Transport.Channels.Embedded;
using DotNetty.Buffers;
using FluentAssertions;
using NSubstitute;
using Serilog;
using Xunit;

namespace Catalyst.Node.Core.IntegrationTests.Rpc.IO.Transport.Channels
{
    public sealed class NodeRpcServerChannelFactoryTests
    {
        private readonly UnitTests.RPC.IO.Transport.Channels.NodeRpcServerChannelFactoryTests.TestNodeRpcServerChannelFactory _serverFactory;
        private readonly NodeRpcClientChannelFactoryTests.TestNodeRpcClientChannelFactory _clientFactory;
        private readonly EmbeddedChannel _serverChannel;
        private readonly EmbeddedChannel _clientChannel;
        private readonly IMessageCorrelationManager _clientCorrelationManager;
        private readonly IKeySigner _clientKeySigner;
        private readonly IAuthenticationStrategy _authenticationStrategy;
        private readonly IPeerIdValidator _peerIdValidator;
        private readonly IKeySigner _serverKeySigner;
        private readonly IMessageCorrelationManager _serverCorrelationManager;

        public NodeRpcServerChannelFactoryTests()
        {
            _serverCorrelationManager = Substitute.For<IMessageCorrelationManager>();
            _serverKeySigner = Substitute.For<IKeySigner>();

            var peerSettings = Substitute.For<IPeerSettings>();
            peerSettings.BindAddress.Returns(IPAddress.Parse("127.0.0.1"));
            peerSettings.Port.Returns(1234);

            _authenticationStrategy = Substitute.For<IAuthenticationStrategy>();

            _peerIdValidator = Substitute.For<IPeerIdValidator>();

            _serverFactory = new UnitTests.RPC.IO.Transport.Channels.NodeRpcServerChannelFactoryTests.TestNodeRpcServerChannelFactory(
                _serverCorrelationManager,
                _serverKeySigner,
                _authenticationStrategy,
                _peerIdValidator);

            _clientCorrelationManager = Substitute.For<IMessageCorrelationManager>();
            _clientKeySigner = Substitute.For<IKeySigner>();
           
            _clientFactory = new NodeRpcClientChannelFactoryTests.TestNodeRpcClientChannelFactory(
                _clientKeySigner, 
                _clientCorrelationManager,
                _peerIdValidator);

            _serverChannel =
                new EmbeddedChannel("server".ToChannelId(), true, _serverFactory.InheritedHandlers.ToArray());
            
            _clientChannel =
                new EmbeddedChannel("client".ToChannelId(), true, _clientFactory.InheritedHandlers.ToArray());
        }

        [Fact]
        public async Task
            NodeRpcServerChannelFactory_Pipeline_Should_Produce_Response_Object_NodeRpcClientChannelFactory_Can_Process()
        {
            var recipient = PeerIdentifierHelper.GetPeerIdentifier("recipient");
            var sender = PeerIdentifierHelper.GetPeerIdentifier("sender");
            var sig = new Signature(ByteUtil.GenerateRandomByteArray(64));
            _peerIdValidator.ValidatePeerIdFormat(Arg.Any<PeerId>()).Returns(true);

            _serverKeySigner.Sign(Arg.Any<byte[]>()).ReturnsForAnyArgs(sig);
            
            var correlationId = CorrelationId.GenerateCorrelationId();

            var protocolMessage = new GetPeerCountResponse().ToProtocolMessage(sender.PeerId, correlationId);
            var dto = new MessageDto<ProtocolMessage>(
                protocolMessage,
                sender,
                recipient,
                CorrelationId.GenerateCorrelationId()
            );

            _clientCorrelationManager.TryMatchResponse(Arg.Any<ProtocolMessage>()).Returns(true);
            
            _serverChannel.WriteOutbound(dto);
            var sentBytes = _serverChannel.ReadOutbound<IByteBuffer>();

            _serverCorrelationManager.DidNotReceiveWithAnyArgs().AddPendingRequest(Arg.Any<CorrelatableMessage>());
            
            _serverKeySigner.ReceivedWithAnyArgs(1).Sign(Arg.Any<byte[]>());
            
            _clientKeySigner.Verify(
                    Arg.Any<PublicKey>(),
                    Arg.Any<byte[]>(),
                    Arg.Any<Signature>())
               .ReturnsForAnyArgs(true);
            
            _authenticationStrategy.Authenticate(Arg.Any<IPeerIdentifier>()).Returns(true);
            
            var observer = new ProtocolMessageObserver(0, Substitute.For<ILogger>());

            var messageStream = _clientFactory.InheritedHandlers.OfType<ObservableServiceHandler>().Single().MessageStream;
            
            using (messageStream.Subscribe(observer))
            {
                _clientChannel.WriteInbound(sentBytes);
                _clientChannel.ReadInbound<ProtocolMessageSigned>();
                _clientCorrelationManager.ReceivedWithAnyArgs(1).TryMatchResponse(Arg.Any<ProtocolMessage>());
                _clientKeySigner.ReceivedWithAnyArgs(1).Verify(null, null, null);
                await messageStream.WaitForItemsOnDelayedStreamOnTaskPoolSchedulerAsync();
                observer.Received.Count.Should().Be(1);
                observer.Received.Single().Payload.CorrelationId.ToCorrelationId().Id.Should().Be(correlationId.Id);
            }
            
            await _serverChannel.DisconnectAsync();
            await _clientChannel.DisconnectAsync();
        }
    }
}
