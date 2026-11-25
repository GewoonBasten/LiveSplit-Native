using Xunit;

namespace LiveSplitNative.App.Tests;

/// <summary>MainWindow tests share AppCli's static settings path, so they never run in parallel.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MainWindowCollection
{
    public const string Name = "MainWindow";
}
