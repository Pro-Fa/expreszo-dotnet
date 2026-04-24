using Expreszo.LanguageServer;

// Launch the ExpresZo Language Server over stdio. Editors that launch this
// binary need no arguments; we read LSP JSON-RPC messages from stdin and
// write responses/notifications to stdout. Logs go to stderr via the
// language-protocol logger wired inside ExpreszoLanguageServer.
using Stream input = Console.OpenStandardInput();
using Stream output = Console.OpenStandardOutput();

await ExpreszoLanguageServer.RunAsync(input, output).ConfigureAwait(false);
