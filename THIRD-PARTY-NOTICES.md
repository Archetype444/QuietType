# Third-party components and data

QuietType's original code is MIT-licensed. The installed distribution includes these free/open-source components and data. Complete notices are in `LICENSES/`.

| Component | Licence | Source |
|---|---|---|
| .NET runtime and Windows Desktop runtime | MIT and included third-party notices | https://github.com/dotnet/runtime and https://github.com/dotnet/winforms |
| ONNX Runtime 1.23.2 | MIT and included third-party notices | https://www.nuget.org/packages/Microsoft.ML.OnnxRuntime/1.23.2 |
| DistilBERT base uncased masked language model | Apache-2.0 | https://huggingface.co/distilbert/distilbert-base-uncased |
| Quantized ONNX conversion of DistilBERT | Apache-2.0, derived from the preceding model | https://huggingface.co/Xenova/distilbert-base-uncased/tree/5d73105e39e322b779ff7a8fcd11530fa579165b |
| English word-frequency dictionary | SymSpell MIT; underlying Google Books Ngram counts CC-BY-3.0 and SCOWL notices | https://github.com/wolfgarbe/SymSpell/tree/91fe939e9c1259813ee817e95239629c5badf8ec |
| Installer engine | Inno Setup licence | https://jrsoftware.org/isinfo.php |

## Model attribution

DistilBERT: Victor Sanh, Lysandre Debut, Julien Chaumond and Thomas Wolf, Hugging Face. See *DistilBERT, a distilled version of BERT: smaller, faster, cheaper and lighter* (2019), https://arxiv.org/abs/1910.01108. The pretrained weights and vocabulary are distributed under Apache-2.0. The bundled INT8 ONNX conversion was published by Xenova; QuietType does not retrain the model. Immutable asset URLs, sizes and SHA-256 hashes are in `assets/asset-manifest.json`.

## Dictionary attribution

The unmodified `frequency_dictionary_en_82_765.txt` is distributed by Wolf Garbe's SymSpell project under its MIT licence. Its upstream README attributes word counts to Google Books Ngram data (Google, https://storage.googleapis.com/books/ngrams/books/datasetsv2.html), licensed under Creative Commons Attribution 3.0, https://creativecommons.org/licenses/by/3.0/, and vocabulary filtering to SCOWL, https://wordlist.aspell.net/. QuietType loads an ASCII subset in memory; the source dictionary is not altered. SCOWL copyright and permission notices are reproduced in `LICENSES/SCOWL-Copyright.txt`.

## Runtime privacy

Official Windows ONNX Runtime binaries support operating-system trace telemetry. QuietType explicitly disables ONNX Runtime telemetry before creating an inference session and disables inference profiling. The application has no networking client or update checker. Windows and the target applications have their own separate data-handling policies.

## Installer

Inno Setup is a free installer with source available under its own permissive licence, with some components under other free licences. The installer is built using the official compiler on GitHub's Windows runner. See `LICENSES/Inno-Setup.txt`. The source compiler is not needed to use QuietType.
