using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    class ServerCapabilities
    {
        [JsonProperty("textDocumentSync")]
        public TextDocumentSyncKind? TextDocumentSync { get; set; }

        [JsonProperty("hoverProvider")]
        public bool? HoverProvider { get; set; }

        [JsonProperty("completionProvider")]
        public CompletionOptions CompletionProvider { get; set; }

        [JsonProperty("referencesProvider")]
        public bool? ReferencesProvider { get; set; }

        [JsonProperty("definitionProvider")]
        public bool? DefinitionProvider { get; set; }

        [JsonProperty("documentSymbolProvider")]
        public bool? DocumentSymbolProvider { get; set; }

        [JsonProperty("workspaceSymbolProvider")]
        public bool? WorkspaceSymbolProvider { get; set; }

        /// <summary>
        /// Code Action options.
        /// </summary>
        public class CodeActionOptions
        {
            /// <summary>
            /// CodeActionKinds that this server may return.
            /// 
            /// The list of kinds may be generic, such as `CodeActionKind.Refactor`, or the server
            /// may list out every specific kind they provide.
            /// </summary>
            public string[] codeActionKinds { get; set; }
        }

        /**
	     * The server provides code actions. The `CodeActionOptions` return type is only
	     * valid if the client signals code action literal support via the property
	     * `textDocument.codeAction.codeActionLiteralSupport`.
	     */
        public CodeActionOptions codeActionProvider { get; set; }

        [JsonProperty("renameProvider")]
        public bool? RenameProvider { get; set; }

        [JsonProperty("colorProvider")]
        public bool? ColorProvider { get; set; }

        [JsonProperty("documentHighlightProvider")]
        public bool? DocumentHighlightProvider { get; set; }

        [JsonProperty("documentFormattingProvider")]
        public bool? DocumentFormattingProvider { get; set; }

        [JsonProperty("documentRangeFormattingProvider")]
        public bool? DocumentRangeFormattingProvider { get; set; }

        [JsonProperty("documentOnTypeFormattingProvider")]
        public DocumentOnTypeFormattingOptions DocumentOnTypeFormattingProvider { get; set; }

        [JsonProperty("signatureHelpProvider")]
        public SignatureHelpOptions SignatureHelpProvider { get; set; }

        /// <summary>
        /// The server provides folding provider support.
        /// 
        /// Since 3.10.0
        /// </summary>
        public bool foldingRangeProvider { get; set; } // boolean | FoldingRangeProviderOptions | (FoldingRangeProviderOptions & TextDocumentRegistrationOptions & StaticRegistrationOptions);

        internal class Workspace
        {
            internal class WorkspaceFolders
            {
                /// <summary>
                /// The server has support for workspace folders
                /// </summary>
                public bool supported { get; set; } = true;

                /// <summary>
                /// Whether the server wants to receive workspace folder
                /// change notifications.
                /// 
                /// If a strings is provided the string is treated as a ID
                /// under which the notification is registered on the client
                /// side.The ID can be used to unregister for these events
                /// using the `client/unregisterCapability` request.
                /// </summary>
                public object changeNotifications { get; set; } = true;
            }

            [JsonProperty("workspaceFolders")]
            public WorkspaceFolders workspaceFolders { get; set; } = new WorkspaceFolders();
        }

        [JsonProperty("workspace")]
        public Workspace workspace { get; set; } = new Workspace();
    }

    [JsonObject]
    class SignatureHelpOptions
    {
        [JsonProperty("triggerCharacters")]
        public char[] TriggerCharacters { get; set; }
    }

    /**
     * Format document on type options
     */
    [JsonObject]
    class DocumentOnTypeFormattingOptions
    {
        /**
         * A character on which formatting should be triggered, like `}`.
         */
        [JsonProperty("firstTriggerCharacter")]
        public char FirstTriggerCharacter { get; set; }

        /**
         * More trigger characters.
         */
        [JsonProperty("moreTriggerCharacter")]
        public char[] MoreTriggerCharacter { get; set; }
    }

    [JsonObject]
    class CompletionOptions
    {
        /// <summary>
        /// The server provides support to resolve additional information for a completion item.
        /// </summary>
        [JsonProperty("resolveProvider")]
        public bool ResolveProvider { get; set; }

        /// <summary>
        /// The characters that trigger completion automatically.
        /// </summary>
        [JsonProperty("triggerCharacters")]
        public char[] TriggerCharacters { get; set; }
    }

    enum TextDocumentSyncKind
    {
        /**
	     * Documents should not be synced at all.
	     */
        None = 0,

        /**
         * Documents are synced by always sending the full content
         * of the document.
         */
        Full = 1,

        /**
         * Documents are synced by sending the full content on open.
         * After that only incremental updates to the document are
         * send.
         */
        Incremental = 2,
    }
}
