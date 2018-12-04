﻿/*

Copyright 2016, 2017, 2018 GIS People Pty Ltd

This file is part of GruntiMaps.

GruntiMaps is free software: you can redistribute it and/or modify it under 
the terms of the GNU Affero General Public License as published by the Free
Software Foundation, either version 3 of the License, or (at your option) any
later version.

GruntiMaps is distributed in the hope that it will be useful, but WITHOUT ANY
WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR
A PARTICULAR PURPOSE. See the GNU Affero General Public License for more 
details.

You should have received a copy of the GNU Affero General Public License along
with GruntiMaps.  If not, see <https://www.gnu.org/licenses/>.

*/
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GruntiMaps.ResourceAccess.Storage
{
    public interface IStorage
    {
        // returns the location of the created file (provider-dependent)
        Task<string> Store(string fileName, string inputPath);
        // returns true if it retrieved a newer version of the file, false if no newer version existed (or an error occurred during the check)
        Task<bool> GetIfNewer(string fileName, string outputPath);
        Task<List<string>> List();
    }
}
