using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.BlockPreview.Extensions;
using Umbraco.Community.BlockPreview.Interfaces;
using Umbraco.Extensions;

namespace Umbraco.Community.BlockPreview.Services
{
    public class BackOfficePreviewServiceBase : IBackOfficePreviewService
    {
        private readonly ITempDataProvider _tempDataProvider;
        private readonly IViewComponentHelperWrapper _viewComponentHelperWrapper;
        private readonly IRazorViewEngine _razorViewEngine;
        private readonly BlockPreviewOptions _options;
        private readonly ITypeFinder _typeFinder;
        private readonly BlockEditorConverter _blockEditorConverter;
        private readonly IViewComponentSelector _viewComponentSelector;

        public BackOfficePreviewServiceBase(
            ITempDataProvider tempDataProvider,
            IViewComponentHelperWrapper viewComponentHelperWrapper,
            IRazorViewEngine razorViewEngine,
            ITypeFinder typeFinder,
            BlockEditorConverter blockEditorConverter,
            IViewComponentSelector viewComponentSelector,
            IOptions<BlockPreviewOptions> options)
        {
            _tempDataProvider = tempDataProvider;
            _viewComponentHelperWrapper = viewComponentHelperWrapper;
            _razorViewEngine = razorViewEngine;
            _typeFinder = typeFinder;
            _blockEditorConverter = blockEditorConverter;
            _viewComponentSelector = viewComponentSelector;
            _options = options.Value;
        }

        public virtual void ConvertNestedValuesToString(BlockItemData? blockData)
        {
            if (blockData == null)
                return;

            foreach (var rawPropValue in blockData.RawPropertyValues.Where(x => x.Value != null))
            {
                var originalValue = rawPropValue.Value;
                if (originalValue.TryConvertToGridItem(out BlockValue? blockValue))
                {
                    blockValue?.ContentData.ForEach(ConvertNestedValuesToString);
                    blockValue?.SettingsData.ForEach(ConvertNestedValuesToString);
                    blockData.RawPropertyValues[rawPropValue.Key] = JsonConvert.SerializeObject(blockValue);
                    continue;
                }
                blockData.RawPropertyValues[rawPropValue.Key] = originalValue?.ToString();
            }
        }
        public virtual IPublishedElement? ConvertToElement(BlockItemData data, bool throwOnError)
        {
            var element = _blockEditorConverter.ConvertToElement(data, PropertyCacheLevel.None, throwOnError);
            if (element == null && throwOnError)
                throw new InvalidOperationException($"Unable to find Element {data?.ContentTypeAlias}");

            return element;
        }

        public virtual Type? FindBlockType(string? contentTypeAlias) =>
            _typeFinder.FindClassesWithAttribute<PublishedModelAttribute>().FirstOrDefault(x =>
                x.GetCustomAttribute<PublishedModelAttribute>(false)?.ContentTypeAlias == contentTypeAlias);

        public virtual ViewDataDictionary CreateViewData(object? typedBlockInstance)
        {
            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = typedBlockInstance
            };

            viewData["blockPreview"] = true;
            return viewData;
        }

        public virtual object? CreateBlockInstance(bool isGrid, Type? contentBlockType, IPublishedElement? contentElement, Type? settingsBlockType, IPublishedElement? settingsElement, Udi? contentUdi, Udi? settingsUdi)
        {
            bool hasSettings = settingsBlockType != null && settingsElement != null;

            if (contentBlockType != null)
            {
                Type blockItemType = isGrid ? typeof(BlockGridItem<>) : typeof(BlockListItem<>);

                if (hasSettings)
                {
                    blockItemType = isGrid ? typeof(BlockGridItem<,>) : typeof(BlockListItem<,>);
                }

                Type blockElementType = !hasSettings ? blockItemType.MakeGenericType(contentBlockType) : blockItemType.MakeGenericType(contentBlockType, settingsBlockType!);

                ConstructorInfo? ctor = blockElementType.GetConstructor(new[]
                {
                    typeof(Udi),
                    contentBlockType,
                    typeof(Udi),
                    settingsElement != null ? settingsBlockType : typeof(IPublishedElement)
                });

                object blockGridItemInstance = ctor!.Invoke(new object[]
                {
                    contentUdi!,
                    contentElement!,
                    settingsUdi!,
                    settingsElement!
                });

                return blockGridItemInstance;
            }

            return null;
        }

        public virtual async Task<string> GetMarkup(ControllerContext controllerContext, string? contentAlias, ViewDataDictionary viewData, bool isGrid = false)
        {
            var viewComponent = _viewComponentSelector.SelectComponent(contentAlias);

            return viewComponent != null
                ? await GetMarkupFromViewComponent(controllerContext, viewData, viewComponent)
                : await GetMarkupFromPartial(controllerContext, viewData, contentAlias, true);
        }

        public virtual async Task<string> GetMarkupFromPartial(
            ControllerContext controllerContext,
            ViewDataDictionary? viewData,
            string? contentAlias,
            bool isGrid = false)
        {
            List<string> viewPaths = isGrid ? _options.ViewLocations.BlockGrid : _options.ViewLocations.BlockList;

            foreach (var viewPath in viewPaths)
            {
                string formattedViewPath = string.Format($"~{viewPath}", contentAlias);
                ViewEngineResult viewResult = _razorViewEngine.GetView("", formattedViewPath, false);

                if (!viewResult.Success)
                {
                    formattedViewPath = string.Format($"~{viewPath}", contentAlias?.ToPascalCase());
                    viewResult = _razorViewEngine.GetView("", formattedViewPath, false);

                    if (!viewResult.Success)
                        continue;
                }

                var actionContext = new ActionContext(controllerContext.HttpContext, new RouteData(), new ActionDescriptor());
                if (viewResult?.View == null)
                    continue;

                await using var sw = new StringWriter();

                if (viewData != null)
                {
                    var viewContext = new ViewContext(actionContext, viewResult.View, viewData,
                        new TempDataDictionary(actionContext.HttpContext, _tempDataProvider), sw, new HtmlHelperOptions());

                    await viewResult.View.RenderAsync(viewContext);
                }

                return sw.ToString();
            }

            return string.Empty;
        }

        public virtual async Task<string> GetMarkupFromViewComponent(
            ControllerContext controllerContext,
            ViewDataDictionary viewData,
            ViewComponentDescriptor viewComponent)
        {
            await using var sw = new StringWriter();
            var viewContext = new ViewContext(
                controllerContext,
                new FakeView(),
                viewData,
                new TempDataDictionary(controllerContext.HttpContext, _tempDataProvider),
                sw,
                new HtmlHelperOptions());

            _viewComponentHelperWrapper.Contextualize(viewContext);

            var result = await _viewComponentHelperWrapper.InvokeAsync(viewComponent.TypeInfo.AsType(), viewData.Model);
            result.WriteTo(sw, HtmlEncoder.Default);
            return sw.ToString();
        }

        public virtual async Task<string> GetMarkupForBlock(
            BlockValue blockValue,
            string dataTypeKey,
            ControllerContext controllerContext,
            string? culture)
        {
            return await Task.FromResult<string>(string.Empty);
        }

        private sealed class FakeView : IView
        {
            public string Path => string.Empty;

            public Task RenderAsync(ViewContext context)
            {
                return Task.CompletedTask;
            }
        }
    }
}