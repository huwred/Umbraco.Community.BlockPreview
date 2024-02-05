﻿using Microsoft.AspNetCore.Http;
using Umbraco.Community.BlockPreview.Controllers;
using Umbraco.Extensions;

namespace Umbraco.Community.BlockPreview.Extensions
{
    public static class HttpRequestExtensions
    {
        public static bool IsBlockPreviewRequest(this HttpRequest request)
        {
            var httpContext = request.HttpContext;

            // We're always going to be coming from the back office so let's check that
            bool isBackOffice = request.IsBackOfficeRequest();

            string requestControllerName = (string)httpContext.Request.RouteValues["controller"] + "Controller";

            bool requestControllerMatches = requestControllerName.Equals(nameof(BlockPreviewApiController));
            bool isBlockGridPreview = httpContext.Request.RouteValues["action"]!.Equals(nameof(BlockPreviewApiController.PreviewGridMarkup));
            bool isBlockListPreview = httpContext.Request.RouteValues["action"]!.Equals(nameof(BlockPreviewApiController.PreviewListMarkup));

            bool isBlockPreviewController = requestControllerMatches && (isBlockGridPreview || isBlockListPreview);

            return isBackOffice && isBlockPreviewController;
        }
    }
}
