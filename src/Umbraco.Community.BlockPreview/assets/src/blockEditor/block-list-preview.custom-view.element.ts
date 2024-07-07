import { UMB_BLOCK_LIST_ENTRY_CONTEXT, UmbBlockListValueModel } from "@umbraco-cms/backoffice/block-list";
import { UmbDocumentWorkspaceContext } from "@umbraco-cms/backoffice/document";
import { css, customElement, html, ifDefined, property, state, unsafeHTML } from "@umbraco-cms/backoffice/external/lit";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import { UMB_PROPERTY_CONTEXT, UMB_PROPERTY_DATASET_CONTEXT } from "@umbraco-cms/backoffice/property";
import { UMB_WORKSPACE_CONTEXT } from "@umbraco-cms/backoffice/workspace";
import { UmbBlockListEntryContext } from "../../node_modules/@umbraco-cms/backoffice/dist-cms/packages/block/block-list/context/block-list-entry.context";
import { BlockPreviewService } from "../api";
import { tryExecuteAndNotify } from "@umbraco-cms/backoffice/resources";
import { UmbBlockEditorCustomViewElement } from "..";

const elementName = "block-list-preview";

@customElement(elementName)
export class BlockListPreviewCustomView
    extends UmbLitElement
    implements UmbBlockEditorCustomViewElement {
        
    #blockListEntryContext?: UmbBlockListEntryContext;

    @state()
    _htmlMarkup: string | undefined = "";

    @state()
    private documentUnique?: string = '';

    @state()
    private _blockEditorAlias?: string = '';

    @state()
    private culture?: string = '';

    @state()
    _workspaceEditContentPath?: string;

    @state()
    private _value: UmbBlockListValueModel = {
        layout: {},
        contentData: [],
        settingsData: []
    }

    @property({ attribute: false })
    public set value(value: UmbBlockListValueModel | undefined) {
        const buildUpValue: Partial<UmbBlockListValueModel> = value ? { ...value } : {};
        buildUpValue.layout ??= {};
        buildUpValue.contentData ??= [];
        buildUpValue.settingsData ??= [];
        this._value = buildUpValue as UmbBlockListValueModel;
    }
    public get value(): UmbBlockListValueModel {
        return this._value;
    }

    constructor() {
        super();

        this.consumeContext(UMB_PROPERTY_CONTEXT, (instance) => {
            this.observe(instance.alias, (alias) => {
                this._blockEditorAlias = alias;
            });
        })

        this.consumeContext(UMB_BLOCK_LIST_ENTRY_CONTEXT, (instance) => {
            this.#blockListEntryContext = instance;

            this.observe(this.#blockListEntryContext.workspaceEditContentPath, (path) => {
                this._workspaceEditContentPath = path;
            });

            this.observe(this.#blockListEntryContext.content, (content) => {
                const contentArr = [content!];
                this._value = { ...this._value, contentData: contentArr };
            });

            this.observe(this.#blockListEntryContext.settings, (settings) => {
                if (settings !== undefined) {
                    const settingsArr = [settings!];
                    this._value = { ...this._value, settingsData: settingsArr };
                }
            });

            this.observe(this.#blockListEntryContext.layout, (layout) => {
                const layoutArr = [layout!];
                this._value = { ...this._value, layout: { ["Umbraco.BlockList"]: layoutArr } };
            });
        });

        this.consumeContext(UMB_PROPERTY_DATASET_CONTEXT, (instance) => {
            this.culture = instance.getVariantId().culture ?? undefined;
        });

        this.consumeContext(UMB_WORKSPACE_CONTEXT, (nodeContext) => {
            const workspaceContext = (nodeContext as UmbDocumentWorkspaceContext);

            this.observe((workspaceContext).unique, (unique) => {
                this.documentUnique = unique;
            });
        });
    }

    async connectedCallback() {
        super.connectedCallback();

        if (this.value != null) {
            const { data } = await tryExecuteAndNotify(this, BlockPreviewService.postUmbracoManagementApiV1BlockPreviewPreviewList({ blockEditorAlias: this._blockEditorAlias, culture: this.culture, pageKey: this.documentUnique, requestBody: JSON.stringify(this.value) }));

            if (data) {
                this._htmlMarkup = data;
            }
        }
    }

    render() {
        if (this._htmlMarkup !== "") {
            return html`
                <a href=${ifDefined(this._workspaceEditContentPath)}>
                    ${unsafeHTML(this._htmlMarkup)}
                </a>`;
        }
        return;
    }

    static styles = [
        css`
            a {
              display: block;
              color: inherit;
              text-decoration: inherit;
              border: 1px solid transparent;
              border-radius: 2px;
            }

            a:hover {
                border-color: var(--uui-color-interactive-emphasis, #3544b1);
            }
        `
    ]
}

export default BlockListPreviewCustomView;

declare global {
    interface HTMLElementTagNameMap {
        [elementName]: BlockListPreviewCustomView;
    }
}
