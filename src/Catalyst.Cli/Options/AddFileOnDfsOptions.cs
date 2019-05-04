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
    /// Add file onto DFS CLI options
    /// </summary>
    [Verb("addfile", HelpText = "Adds a file onto the DFS")]
    public sealed class AddFileOnDfsOptions : IAddFileOnDfsOptions
    {
        /// <inheritdoc />
        [Option('n', "node", HelpText = "A valid node ID as listed in the nodes.json config file.")]
        public string Node { get; set; }

        /// <inheritdoc />
        [Option('f', "file", HelpText = "The file to upload onto DFS")]
        public string File { get; set; }

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
                new Example("Adds a file to the DFS.", new AddFileOnDfsOptions {Node = "node1", File = "AFile.txt"})
            };
    }
}
