﻿using System;
using System.Threading.Tasks;
using GruntiMaps.Api.DataContracts.V2;
using GruntiMaps.Api.DataContracts.V2.Layers;
using GruntiMaps.Common.Enums;
using GruntiMaps.WebAPI.Interfaces;
using GruntiMaps.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Gruntify.Api.Common.Services;

namespace GruntiMaps.WebAPI.Controllers.Layers
{
    public class CreateLayerController : ApiControllerBase
    {
        private readonly IMapData _mapData;
        private readonly IResourceLinksGenerator _resourceLinksGenerator;

        public CreateLayerController(IMapData mapData,
            IResourceLinksGenerator resourceLinksGenerator)
        {
            _mapData = mapData;
            _resourceLinksGenerator = resourceLinksGenerator;
        }

        [HttpPost(Resources.Layers)]
        public async Task<LayerDto> Invoke([FromBody] CreateLayerDto dto)
        {
            var id = Guid.NewGuid().ToString();
            ConversionMessageData messageData = new ConversionMessageData
            {
                LayerId = id,
                LayerName = dto.Name,
                DataLocation = dto.DataLocation,
                Description = dto.Description
            };
            await _mapData.CreateGdalConversionRequest(messageData);
            await _mapData.JobStatusTable.UpdateStatus(id, LayerStatus.Processing);
            return new LayerDto()
            {
                Id = id,
                Status = LayerStatus.Processing,
                Links = _resourceLinksGenerator.GenerateResourceLinks(id),
            };
        }
    }
}