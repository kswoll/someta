# ObservableAsProperty

ReactiveUI provides a Fody extension (that I originally authored) the provides this functionality, but using this framework, you can just do it yourself, if you wanted. It provides a good example of the sort of extensions you can author with minimal fuss:

snippet: ObservableAsProperty

Here's the equivalent implementation to ObservableAsPropertyExtensions: (**note**: this is a simplified example without all the overloads, but you could easily add the missing ones)

snippet: ObservableAsPropertyExtensions

Usage:

snippet: ObservableAsPropertyExample