// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Microsoft.AspNetCore.Server.Kestrel.Core.Internal
{
    internal class ServerAddressesFeature : IServerAddressesFeature
    {
        private ICollection<string> _addresses = new List<string>();
        private bool _blockExternalAddressMutation;

        public ICollection<string> Addresses
        {
            get
            {
                // For better thread safety, give each consumer it's own copy after the server starts.
                // Without a custom ICollection, there's no guarantee that the collection isn't being modified
                // by a caller who accessed addresses before mutation was blocked, so locking here wouldn't give us much.
                return _blockExternalAddressMutation ? new List<string>(_addresses) : _addresses;
            }
            internal set
            {
                // For thread safety, the ICollection should not be modified after it is set.
                _addresses = value;
            }
        }

        public bool PreferHostingUrls { get; set; }

        internal void BlockExternalAddressesMutation()
        {
            _blockExternalAddressMutation = true;
        }
    }
}
