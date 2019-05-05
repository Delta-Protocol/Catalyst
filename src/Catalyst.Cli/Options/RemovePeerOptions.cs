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

using System.Collections.Generic;
using Catalyst.Common.Interfaces.Cli.Options;
using CommandLine;
using CommandLine.Text;

namespace Catalyst.Cli.Options
{
    /// <summary>
    /// Class contains the options for the peer list command
    /// </summary>
    [Verb("removepeer", HelpText = "removes a peer")]
    internal sealed class RemovePeerOptions : IRemovePeerOptions
    {
        /// <inheritdoc />
        [Option('n', "node", HelpText = "A valid node ID as listed in the nodes.json config file.", Required = true)]
        public string Node { get; set; }

        /// <inheritdoc />
        [Option('k', "publickey", HelpText = "The peer public key", Required = false)]
        public string PublicKey { get; set; }

        /// <inheritdoc />
        [Option('i', "ip", HelpText = "The peer IP", Required = true)]
        public string Ip { get; set; }

        /// <summary>
        /// Gets the examples.
        /// </summary>
        /// <value>
        /// The examples.
        /// </value>
        [Usage(ApplicationAlias = "")]
        public static IEnumerable<Example> Examples =>
            new List<Example>
            {
                new Example("Removes a peer from the specified node.",
                    new RemovePeerOptions {Ip = "127.0.0.1", Node = "node1", PublicKey = "302a300506032b657003"})
            };
    }
}