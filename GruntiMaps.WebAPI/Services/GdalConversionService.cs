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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using GruntiMaps.WebAPI.DataContracts;
using GruntiMaps.WebAPI.Interfaces;
using GruntiMaps.WebAPI.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace GruntiMaps.WebAPI.Services
{
    public class GdalConversionService : BackgroundService
    {
        private readonly ILogger<GdalConversionService> _logger;
        private readonly IMapData _mapdata;
        private readonly Options _options;
        private readonly List<string> _supportedFileTypes;
//        private static DocumentClient _client;

        /// <summary>
        ///     Create a new GdalConversionService instance.
        /// </summary>
        /// <param name="logger">system logger</param>
        /// <param name="options">global options for the Map Server</param>
        /// <param name="mapdata">Map data layers</param>
        public GdalConversionService(ILogger<GdalConversionService> logger, Options options, IMapData mapdata)
        {
            _logger = logger;
            _mapdata = mapdata;
            _options = options;
            _supportedFileTypes = new List<string> { ".shp", ".geojson", ".gdb" };
//            var EndpointUrl = "https://localhost:8081";
//            var AuthorisationKey = "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";
//            _client = new DocumentClient(new Uri(EndpointUrl), AuthorisationKey);
        }

        protected override async Task Process()
        {
            //            _logger.LogDebug("GDALConversion process starting.");

            // For the enlightenment of other, later, readers: 
            // ogr2ogr will be used to process not only obvious conversion sources (eg shape files) but also
            // geojson files. Why, you might ask, because tippecanoe can import GeoJSON directly? It's because
            // passing the GeoJSON through ogr2ogr will ensure that the final GeoJSON is in the correct projection
            // and that it should be valid GeoJSON as well.
            var gdalMsg = await _mapdata.GdConversionQueue.GetMessage();
            if (gdalMsg != null) // if no message, don't try
            {
                ConversionMessageData gdalData = null;
                try
                {
                    try
                    {
                        gdalData = JsonConvert.DeserializeObject<ConversionMessageData>(gdalMsg.Content);
                    }
                    catch (JsonReaderException e)
                    {
                        _logger.LogError($"failed to decode JSON message on queue {e}");
                        return;
                    }

                    var start = DateTime.UtcNow;
                    _logger.LogDebug("About to convert Gdal");

                    var randomFolderName = Path.GetRandomFileName();
                    _logger.LogDebug($"folder name = {randomFolderName}");
                    // it will be in the system's temporary directory
                    var tempPath = Path.Combine(Path.GetTempPath(), randomFolderName);
                    _logger.LogDebug($"temporary path = {tempPath}");

                    // If the directory already existed, throw an error - this should never happen, but just in case.
                    if (Directory.Exists(tempPath))
                    {
                        _logger.LogError($"The temporary path '{tempPath}' already existed.");
                        throw new Exception("The temporary path already existed.");
                    }

                    // Try to create the directory.
                    Directory.CreateDirectory(tempPath);
                    _logger.LogDebug($"The directory was created successfully at {Directory.GetCreationTime(tempPath)}.");

                    // we need to keep source and dest separate in case there's a collision in filenames.
                    var sourcePath = Path.Combine(tempPath, "source");
                    Directory.CreateDirectory(sourcePath);

                    var destPath = Path.Combine(tempPath, "dest");
                    Directory.CreateDirectory(destPath);

                    if (gdalData.DataLocation != null) // if it was null we don't want to do anything except remove the job from queue
                    {
                        // retrieve the source data file from the supplied URI 
                        var remoteUri = new Uri(gdalData.DataLocation);
                        // we will need to know if this is a supported file type
                        var fileType = Path.GetExtension(remoteUri.AbsolutePath).ToLower();
                        if (!_supportedFileTypes.Contains(fileType))
                        {
                            throw new Exception($"Unsupported file type: {fileType}");
                        }

                        var localFile = Path.Combine(sourcePath, Path.GetFileName(remoteUri.AbsolutePath));
                        WebClient myWebClient = new WebClient();
                        _logger.LogDebug($"Downloading {gdalData.DataLocation} to {localFile}");
                        myWebClient.DownloadFile(gdalData.DataLocation, localFile);

                        var geoJsonFile = Path.Combine(destPath, $"{gdalData.LayerId}.geojson");
                        var gdalProcess = new Process
                        {
                            StartInfo = {
                                FileName = "ogr2ogr",
                                Arguments =
                                    "-f \"GeoJSON\" " +    // always converting to GeoJSON
                                    $"-nln \"{gdalData.LayerName}\" " +
                                    "-t_srs \"EPSG:4326\" " +  // always transform to WGS84
                                    $"{geoJsonFile} " +
                                    $"{localFile}",
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true
                            }
                        };
                        _logger.LogDebug($"ogr2ogr arguments are {gdalProcess.StartInfo.Arguments}");

                        gdalProcess.Start();
                        var errmsg = "";
                        while (!gdalProcess.StandardError.EndOfStream) errmsg += gdalProcess.StandardError.ReadLine();
                        gdalProcess.WaitForExit();
                        var exitCode = gdalProcess.ExitCode;
                        _logger.LogDebug($"og2ogr returned exit code {exitCode}");
                        if (exitCode != 0)
                        {
                            _logger.LogError($"Spatial data to GeoJSON conversion failed (errcode={exitCode}), msgs = {errmsg}");
                            throw new Exception($"Spatial data to GeoJSON conversion failed. {errmsg}");
                        }


                        _logger.LogDebug($"geojson file is in {geoJsonFile}");
                        // now we need to put the converted geojson file into storage
                        var location = await _mapdata.GeojsonContainer.Store($"{gdalData.LayerId}.geojson", geoJsonFile);
                        _logger.LogDebug("Upload of geojson file to storage complete.");

                        var end = DateTime.UtcNow;
                        var duration = end - start;
                        _logger.LogDebug($"GDALConversion took {duration.TotalMilliseconds} ms.");

                        // we created geoJson so we can put a request in for geojson to mvt conversion.
                        await _mapdata.CreateMbConversionRequest(new ConversionMessageData
                        {
                            LayerId = gdalData.LayerId,
                            DataLocation = location,
                            Description = gdalData.Description,
                            LayerName = gdalData.LayerName
                        });
                    }
                    // we completed GDAL conversion and creation of MVT conversion request, so remove the GDAL request from the queue
                    _logger.LogDebug("deleting gdal message from queue");
                    await _mapdata.GdConversionQueue.DeleteMessage(gdalMsg);
                }
                catch (Exception ex)
                {
                    if (gdalData != null)
                    {
                        await _mapdata.JobStatusTable.UpdateStatus(gdalData.LayerId, LayerStatus.Failed);
                    }
                    throw ex;
                }
            }
            await Task.Delay(_options.CheckConvertTime);
        }
        //        }

    }
}
