using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Catalyst.Abstractions.Dfs.CoreApi;
using Microsoft.AspNetCore.Mvc;

namespace Catalyst.Core.Modules.Dfs.WebApi.V0.Controllers
{
    /// <summary>
    ///  A list of peers.
    /// </summary>
    public class BootstrapPeersDto
    {
        /// <summary>
        ///   The multiaddress of a peer.
        /// </summary>
        public IEnumerable<string> Peers;
    }

    /// <summary>
    ///   Manages the list of initial peers.
    /// </summary>
    /// <remarks>
    ///  The API manipulates the "bootstrap list", which contains
    ///  the addresses of the bootstrap nodes. These are the trusted peers from
    ///  which to learn about other peers in the network.
    /// </remarks>
    public class BootstrapController : IpfsController
    {
        /// <summary>
        ///   Creates a new controller.
        /// </summary>
        public BootstrapController(ICoreApi ipfs) : base(ipfs) { }

        /// <summary>
        ///   List all the bootstrap peers.
        /// </summary>
        [HttpGet, HttpPost, Route("bootstrap/list")]
        public async Task<BootstrapPeersDto> List()
        {
            var peers = await IpfsCore.BootstrapApi.ListAsync(Cancel);
            return new BootstrapPeersDto
            {
                Peers = peers.Select(peer => peer.ToString())
            };
        }

        /// <summary>
        ///   Remove all the bootstrap peers.
        /// </summary>
        [HttpGet, HttpPost, Route("bootstrap/rm/all")]
        public async Task RemoveAll() { await IpfsCore.BootstrapApi.RemoveAllAsync(Cancel); }

        /// <summary>
        ///   Add the default bootstrap peers.
        /// </summary>
        [HttpGet, HttpPost, Route("bootstrap/add/default")]
        public async Task<BootstrapPeersDto> AddDefaults()
        {
            var peers = await IpfsCore.BootstrapApi.AddDefaultsAsync(Cancel);
            return new BootstrapPeersDto
            {
                Peers = peers.Select(peer => peer.ToString())
            };
        }

        /// <summary>
        ///   Add a bootstrap peer.
        /// </summary>
        /// <param name="arg">
        ///   The multiaddress of the peer.
        /// </param>
        /// <param name="default">
        ///   If <b>true</b>, add all the default bootstrap peers.
        /// </param>
        [HttpGet, HttpPost, Route("bootstrap/add")]
        public async Task<BootstrapPeersDto> Add(string arg,
            bool @default = false)
        {
            if (@default)
            {
                var peers = await IpfsCore.BootstrapApi.AddDefaultsAsync(Cancel);
                return new BootstrapPeersDto
                {
                    Peers = peers.Select(p => p.ToString())
                };
            }

            var peer = await IpfsCore.BootstrapApi.AddAsync(arg, Cancel);
            return new BootstrapPeersDto
            {
                Peers = new[] {peer?.ToString()}
            };
        }

        /// <summary>
        ///   Remove a bootstrap peer.
        /// </summary>
        /// <param name="arg">
        ///   The multiaddress of the peer.
        /// </param>
        /// <param name="all">
        ///   If <b>true</b>, remove all the bootstrap peers.
        /// </param>
        [HttpGet, HttpPost, Route("bootstrap/rm")]
        public async Task<BootstrapPeersDto> Remove(string arg,
            bool all = false)
        {
            if (all)
            {
                await IpfsCore.BootstrapApi.RemoveAllAsync(Cancel);
                return new BootstrapPeersDto {Peers = new string[0]};
            }

            var peer = await IpfsCore.BootstrapApi.RemoveAsync(arg, Cancel);
            return new BootstrapPeersDto
            {
                Peers = new[] {peer?.ToString()}
            };
        }
    }
}