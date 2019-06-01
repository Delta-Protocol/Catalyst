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

using Catalyst.Common.Util;
using Catalyst.Protocol.Common;
using Catalyst.Protocol.Delta;

namespace Catalyst.Common.UnitTests.TestUtils
{
    public static class CandidateDeltaHelper
    {
        public static CandidateDeltaBroadcast GetCandidateDelta(byte[] previousDeltaHash = null, 
            byte[] hash = null,
            PeerId producerId = null)
        {
            var candidateHash = hash ?? ByteUtil.GenerateRandomByteArray(32);
            var previousHash = previousDeltaHash ?? ByteUtil.GenerateRandomByteArray(32);
            var producer = producerId 
             ?? PeerIdHelper.GetPeerId(publicKey: ByteUtil.GenerateRandomByteArray(32));

            return new CandidateDeltaBroadcast
            {
                Hash = candidateHash.ToByteString(),
                PreviousDeltaDfsHash = previousHash.ToByteString(),
                ProducerId = producer
            };
        }
    }
}