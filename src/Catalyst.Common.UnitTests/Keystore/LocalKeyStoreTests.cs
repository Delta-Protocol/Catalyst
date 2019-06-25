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

using Catalyst.Common.Cryptography;
using Catalyst.Common.Interfaces.Cryptography;
using Catalyst.Common.Interfaces.Keystore;
using Catalyst.Common.Keystore;
using Catalyst.Common.Util;
using Catalyst.Cryptography.BulletProofs.Wrapper;
using Catalyst.TestUtils;
using Multiformats.Hash.Algorithms;
using Nethereum.Hex.HexConvertors.Extensions;
using NSubstitute;
using Serilog;
using Xunit;
using Xunit.Abstractions;

namespace Catalyst.Common.UnitTests.Keystore
{
    public sealed class LocalKeyStoreTests : ConfigFileBasedTest
    {
        private readonly IKeyStore _keystore;
        private readonly ICryptoContext _context;

        public LocalKeyStoreTests(ITestOutputHelper output) : base(output)
        {
            _context = new RustCryptoContext(new CryptoWrapper());

            var logger = Substitute.For<ILogger>();
            var passwordReader = new TestPasswordReader("testPassword");

            var multiAlgo = Substitute.For<IMultihashAlgorithm>();
            multiAlgo.ComputeHash(Arg.Any<byte[]>()).Returns(new byte[32]);

            var addressHelper = new AddressHelper(multiAlgo);

            _keystore = new LocalKeyStore(passwordReader,
                _context,
                new KeyStoreServiceWrapped(_context),
                FileSystem,
                logger,
                addressHelper);
        }

        [Fact]
        public void Should_Generate_Account_And_Create_KeyStore_File_Scrypt()
        {
            var catKey = _context.GeneratePrivateKey();

            var json = _keystore.KeyStoreGenerateAsync(catKey, "testPassword").GetAwaiter().GetResult();
            var key = _keystore.KeyStoreDecrypt("testPassword", json);
            Assert.Equal(catKey.Bytes.RawBytes.ToHex(), key.ToHex());
        }
    }
}
