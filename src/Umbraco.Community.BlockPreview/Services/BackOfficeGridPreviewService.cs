using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.BlockPreview.Interfaces;
using Umbraco.Extensions;

namespace Umbraco.Community.BlockPreview.Services
{
    public sealed class BackOfficeGridPreviewService : BackOfficePreviewServiceBase, IBackOfficeGridPreviewService
    {
        private readonly ContextCultureService _contextCultureService;
        private readonly IContentTypeService _contentTypeService;
        private readonly IDataTypeService _dataTypeService;
        private readonly IJsonSerializer _jsonSerializer;

        public BackOfficeGridPreviewService(
            BlockEditorConverter blockEditorConverter,
            ContextCultureService contextCultureService,
            ITempDataProvider tempDataProvider,
            ITypeFinder typeFinder,
            IViewComponentHelperWrapper viewComponentHelperWrapper,
            IViewComponentSelector viewComponentSelector,
            IOptions<BlockPreviewOptions> options,
            IContentTypeService contentTypeService,
            IDataTypeService dataTypeService,
            IJsonSerializer jsonSeralizer,
            IRazorViewEngine razorViewEngine) : base(tempDataProvider, viewComponentHelperWrapper, razorViewEngine, typeFinder, blockEditorConverter, viewComponentSelector, options)
        {
            _contextCultureService = contextCultureService;
            _contentTypeService = contentTypeService;
            _dataTypeService = dataTypeService;
            _jsonSerializer = jsonSeralizer;
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
            BlockGridLayoutItem? layoutData = JsonConvert.DeserializeObject<BlockGridLayoutItem>(JsonConvert.SerializeObject(blockValue.Layout));

            if (contentData != null)
            {
                ConvertNestedValuesToString(contentData);

                IPublishedElement? contentElement = ConvertToElement(contentData, true);
                string? contentElementTypeAlias = contentElement?.ContentType.Alias;

                IPublishedElement? settingsElement = settingsData != null ? ConvertToElement(settingsData, true) : default;
                string? settingsElementTypeAlias = settingsElement?.ContentType.Alias;

                Type? contentBlockType = FindBlockType(contentElementTypeAlias);
                Type? settingsBlockType = settingsElement != null ? FindBlockType(settingsElementTypeAlias) : default;

                object? blockInstance = CreateBlockInstance(true, contentBlockType, contentElement, settingsBlockType, settingsElement, contentData.Udi, settingsData?.Udi);

                BlockGridItem? typedBlockInstance = blockInstance as BlockGridItem;

                UpdateBlockGridItem(dataTypeKey, contentElement.ContentType.Key, typedBlockInstance, layoutData);

                ViewDataDictionary? viewData = CreateViewData(blockInstance);

                return await GetMarkup(controllerContext, contentElementTypeAlias, viewData, true);
            }

            return string.Empty;
        }

        private void UpdateBlockGridItem(string dataTypeKey, Guid contentElementTypeKey, BlockGridItem? typedBlockInstance, BlockGridLayoutItem? layoutData)
        {
            var dataType = _dataTypeService.GetDataType(Guid.Parse(dataTypeKey));
            var config = dataType.ConfigurationAs<BlockGridConfiguration>();

            typedBlockInstance.RowSpan = layoutData.RowSpan.GetValueOrDefault();
            typedBlockInstance.ColumnSpan = layoutData.ColumnSpan.GetValueOrDefault();

            typedBlockInstance.GridColumns = config?.GridColumns;

            var blockConfig = config?.Blocks.FirstOrDefault(x => x.ContentElementTypeKey.Equals(contentElementTypeKey));
            var blockConfigAreaMap = blockConfig.Areas.ToDictionary(area => area.Key);

            typedBlockInstance.Areas = layoutData.Areas.Select(area =>
            {
                if (!blockConfigAreaMap.TryGetValue(area.Key, out BlockGridConfiguration.BlockGridAreaConfiguration? areaConfig))
                {
                    return null;
                }

                var items = area.Items.Select(item => new BlockGridItem(item.ContentUdi, null, item.SettingsUdi, null)).ToList();
                return new BlockGridArea(items, areaConfig.Alias!, areaConfig.RowSpan!.Value, areaConfig.ColumnSpan!.Value);
            }).WhereNotNull().ToArray();
            typedBlockInstance.AreaGridColumns = blockConfig?.AreaGridColumns;
        }

        public override ViewDataDictionary CreateViewData(object? typedBlockInstance)
        {
            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = typedBlockInstance
            };

            viewData["blockPreview"] = true;
            viewData["blockGridPreview"] = true;
            return viewData;
        }
    }
}