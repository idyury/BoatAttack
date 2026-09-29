This is an embedded copy of Gameplay Ingredients 2020.2.10, retaining the original
asset GUIDs and runtime behavior used by Boat Attack.

Unity 6 compatibility patch: `Editor/Discover/DiscoverWindow.cs` uses
`[field: SerializeField]` on the `discoverAsset` auto-property. `SerializeField`
targets its backing field; applying it to the property is a compiler error.

The custom Scene view overlay also implements `ICreateHorizontalToolbar`, as
required for its advertised horizontal layout by Unity 6.

Keep this package embedded so the fixes survive deleting or rebuilding Library.
