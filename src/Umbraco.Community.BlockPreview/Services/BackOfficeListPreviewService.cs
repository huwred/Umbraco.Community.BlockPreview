using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Community.BlockPreview.Interfaces;

namespace Umbraco.Community.BlockPreview.Services
{
    public sealed class BackOfficeListPreviewService : BackOfficePreviewServiceBase, IBackOfficeListPreviewService
    {
        private readonly ContextCultureService _contextCultureService;

        public BackOfficeListPreviewService(
            BlockEditorConverter blockEditorConverter,
            ContextCultureService contextCultureService,
            ITempDataProvider tempDataProvider,
            ITypeFinder typeFinder,
            IViewComponentHelperWrapper viewComponentHelperWrapper,
            IViewComponentSelector viewComponentSelector,
            IOptions<BlockPreviewOptions> options,
            IRazorViewEngine razorViewEngine) : base(tempDataProvider, viewComponentHelperWrapper, razorViewEngine, typeFinder, blockEditorConverter, viewComponentSelector, options)
        {            
            _contextCultureService = contextCultureService;
        }

        public override async Task<string> GetMarkupForBlock(
            BlockValue blockValue,
            string dataTypeKey,
            ControllerContext controllerContext,
            string? culture)
        {
            if (!string.IsNullOrEmpty(culture))
            {
                _contextCultureService.SetCulture(culture);
            }

            BlockItemData? contentData = blockValue.ContentData.FirstOrDefault();
            BlockItemData? settingsData = blockValue.SettingsData.FirstOrDefault();
            KeyValuePair<string, JToken>? layoutData = blockValue.Layout.FirstOrDefault();

            if (contentData != null)
            {
                ConvertNestedValuesToString(contentData);

                IPublishedElement? contentElement = ConvertToElement(contentData, true);
                string? contentElementTypeAlias = contentElement?.ContentType.Alias;

                IPublishedElement? settingsElement = settingsData != null ? ConvertToElement(settingsData, true) : default;
                string? settingsElementTypeAlias = settingsElement?.ContentType.Alias;

                Type? contentBlockType = FindBlockType(contentElementTypeAlias);
                Type? settingsBlockType = settingsElement != null ? FindBlockType(settingsElementTypeAlias) : default;

                object? blockInstance = CreateBlockInstance(false, contentBlockType, contentElement, settingsBlockType, settingsElement, contentData.Udi, settingsData?.Udi);

                BlockListItem? typedBlockInstance = blockInstance as BlockListItem;

                ViewDataDictionary? viewData = CreateViewData(typedBlockInstance);

                return await GetMarkup(controllerContext, contentElementTypeAlias, viewData);
            }

            return string.Empty;
        }
    }
}