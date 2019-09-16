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

using Autofac;
using Catalyst.Abstractions.IO.Observers;
using Catalyst.Abstractions.Rpc;
using Catalyst.Core.Modules.Rpc.Server.IO.Observers;

namespace Catalyst.Core.Modules.Rpc.Server
{
    public class RpcServerModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<RpcServer>().As<IRpcServer>().SingleInstance();
            builder.RegisterType<RpcServerSettings>().As<IRpcServerSettings>();

            /**
             * Rpc Server Observers
             **/
            builder.RegisterType<AddFileToDfsRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<BroadcastRawTransactionRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<ChangeDataFolderRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<GetDeltaRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<GetFileFromDfsRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<GetInfoRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<GetMempoolRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<GetPeerInfoRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<GetVersionRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<PeerBlackListingRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<PeerCountRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<PeerListRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<PeerReputationRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<RemovePeerRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<SignMessageRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<TransferFileBytesRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
            builder.RegisterType<VerifyMessageRequestObserver>().As<IRpcRequestObserver>().SingleInstance();
        }  
    }
}