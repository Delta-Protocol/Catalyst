﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Catalyst.Abstractions.Dfs.CoreApi;
using Catalyst.Abstractions.Options;
using MultiFormats;
using Newtonsoft.Json.Linq;

namespace Catalyst.Core.Modules.Dfs.CoreApi
{
    internal sealed class BootstrapApi : IBootstrapApi
    {
        // From https://github.com/libp2p/go-libp2p-daemon/blob/master/bootstrap.go#L14
        private static readonly MultiAddress[] Defaults = 
        {
            "/ip4/104.131.131.82/tcp/4001/ipfs/QmaCpDMGvV2BGHeYERUEnRQAwe3N8SzbUtfsmvsqQLuvuJ",            // mars.i.ipfs.io
            "/ip4/104.236.179.241/tcp/4001/ipfs/QmSoLPppuBtQSGwKDZT2M73ULpjvfd3aZ6ha4oFGL1KrGM",           // pluto.i.ipfs.io
            "/ip4/128.199.219.111/tcp/4001/ipfs/QmSoLSafTMBsPKadTEgaXctDQVcqN88CNLHXMkTNwMKPnu",           // saturn.i.ipfs.io
            "/ip4/104.236.76.40/tcp/4001/ipfs/QmSoLV4Bbm51jM9C4gDYZQ9Cy3U6aXMJDAbzgu2fzaDs64",             // venus.i.ipfs.io
            "/ip4/178.62.158.247/tcp/4001/ipfs/QmSoLer265NRgSp2LA3dPaeykiS1J6DifTC88f5uVQKNAd",            // earth.i.ipfs.io
            "/ip6/2604:a880:1:20::203:d001/tcp/4001/ipfs/QmSoLPppuBtQSGwKDZT2M73ULpjvfd3aZ6ha4oFGL1KrGM",  // pluto.i.ipfs.io
            "/ip6/2400:6180:0:d0::151:6001/tcp/4001/ipfs/QmSoLSafTMBsPKadTEgaXctDQVcqN88CNLHXMkTNwMKPnu",  // saturn.i.ipfs.io
            "/ip6/2604:a880:800:10::4a:5001/tcp/4001/ipfs/QmSoLV4Bbm51jM9C4gDYZQ9Cy3U6aXMJDAbzgu2fzaDs64", // venus.i.ipfs.io
            "/ip6/2a03:b0c0:0:1010::23:1001/tcp/4001/ipfs/QmSoLer265NRgSp2LA3dPaeykiS1J6DifTC88f5uVQKNAd", // earth.i.ipfs.io
        };

        private readonly IConfigApi _configApi;
        private readonly DiscoveryOptions _discoveryOptions;

        public BootstrapApi(IConfigApi configApi, DiscoveryOptions discoveryOptions)
        {
            _configApi = configApi;
            _discoveryOptions = discoveryOptions;
        }

        public async Task<MultiAddress> AddAsync(MultiAddress address,
            CancellationToken cancel = default)
        {
            // Throw if missing peer ID
            var _ = address.PeerId;

            var addrs = (await ListAsync(cancel).ConfigureAwait(false)).ToList();
            if (addrs.Any(a => a == address))
            {
                return address;
            }

            addrs.Add(address);
            var strings = addrs.Select(a => a.ToString());
            await _configApi.SetAsync("Bootstrap", JToken.FromObject(strings), cancel).ConfigureAwait(false);
            return address;
        }

        public async Task<IEnumerable<MultiAddress>> AddDefaultsAsync(CancellationToken cancel =
            default)
        {
            foreach (var a in Defaults)
            {
                await AddAsync(a, cancel).ConfigureAwait(false);
            }

            return Defaults;
        }

        public async Task<IEnumerable<MultiAddress>> ListAsync(CancellationToken cancel = default)
        {
            if (_discoveryOptions.BootstrapPeers != null)
            {
                return _discoveryOptions.BootstrapPeers;
            }

            try
            {
                var json = await _configApi.GetAsync("Bootstrap", cancel);
                return json == null ? new MultiAddress[0] : json.Select(a => MultiAddress.TryCreate((string) a)).Where(a => a != null);
            }
            catch (KeyNotFoundException)
            {
                var strings = Defaults.Select(a => a.ToString());
                await _configApi.SetAsync("Bootstrap", JToken.FromObject(strings), cancel).ConfigureAwait(false);
                return Defaults;
            }
        }

        public async Task RemoveAllAsync(CancellationToken cancel = default)
        {
            await _configApi.SetAsync("Bootstrap", JToken.FromObject(new string[0]), cancel).ConfigureAwait(false);
        }

        public async Task<MultiAddress> RemoveAsync(MultiAddress address,
            CancellationToken cancel = default)
        {
            var addrs = (await ListAsync(cancel).ConfigureAwait(false)).ToList();
            if (addrs.All(a => a != address))
            {
                return address;
            }

            addrs.Remove(address);
            var strings = addrs.Select(a => a.ToString());
            await _configApi.SetAsync("Bootstrap", JToken.FromObject(strings), cancel).ConfigureAwait(false);
            return address;
        }
    }
}