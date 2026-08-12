namespace FormTesting.Client.Tests;

/// <summary>
/// The xUnit collection shared by every test class that touches the process-static
/// <see cref="WasmMessageService"/>/<see cref="WasmNotificationService"/>. xUnit parallelizes
/// across collections (one collection per class by default), so without this, two such classes can
/// interleave and one's <c>Clear()</c> wipes the other's in-flight toast mid-assertion — a
/// schedule-dependent flake that surfaces as "element not found" in whichever class loses the race.
/// Membership rule: any test that calls a <c>Wasm*Service</c> API or renders a
/// <c>Wasm*Container</c> belongs in this collection.
/// </summary>
[CollectionDefinition(Name)]
public static class WasmStaticToastCollection
{
    public const string Name = "Wasm static toast services";
}
