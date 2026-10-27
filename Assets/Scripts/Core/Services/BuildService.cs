using System.Collections.Generic;

namespace Bush.Core
{
    /// <summary>
    /// What's placed where. Placing takes the item from the inventory and putting it away gives it
    /// back, both through <see cref="Economy"/>. Where things are allowed to go (on the floor, not
    /// blocking paths) is checked by the game layer before calling this, because it needs physics.
    /// </summary>
    public class BuildService
    {
        private readonly DesignData _design;
        private readonly GameState _state;
        private readonly Economy _economy;
        private readonly GameEvents _events;

        public BuildService(DesignData design, GameState state, Economy economy, GameEvents events)
        {
            _design = design;
            _state = state;
            _economy = economy;
            _events = events;
        }

        /// <summary>
        /// Prototype mode: anything can be placed without owning it. Placing doesn't use items and
        /// putting those away doesn't give items back. Turn it off once placeables are earned.
        /// </summary>
        public bool UnlimitedItems { get; set; }

        /// <summary>How many more of this can be placed. int.MaxValue in unlimited mode.</summary>
        public int CountAvailable(string placeableId)
        {
            PlaceableData placeable = _design.GetPlaceable(placeableId);
            if (placeable == null) return 0;
            return UnlimitedItems ? int.MaxValue : _economy.Count(placeable.itemId);
        }

        public bool CanPlace(string placeableId) => CountAvailable(placeableId) > 0;

        public IEnumerable<PlacedObjectData> GetPlaced(string area)
        {
            foreach (PlacedObjectData placed in _state.placed)
                if (placed.area == area) yield return placed;
        }

        public PlacedObjectData Find(int instanceId) => _state.placed.Find(p => p.instanceId == instanceId);

        /// <summary>Places one, taking it from the inventory unless unlimited. Returns null if it can't.</summary>
        public PlacedObjectData Place(string area, string placeableId, float x, float y, bool flipped)
        {
            PlaceableData placeable = _design.GetPlaceable(placeableId);
            if (placeable == null || !CanPlace(placeableId)) return null;

            bool fromInventory = !UnlimitedItems;
            if (fromInventory && !_economy.TryRemove(placeable.itemId, 1)) return null;

            PlacedObjectData placed = Add(area, placeable, x, y, flipped, fromInventory);
            _events?.RaiseObjectPlaced(placeable.itemId);
            _events?.RaiseBuildChanged(area);
            return placed;
        }

        public bool Move(int instanceId, float x, float y)
        {
            PlacedObjectData placed = Find(instanceId);
            if (placed == null) return false;
            placed.x = x;
            placed.y = y;
            _events?.RaiseBuildChanged(placed.area);
            return true;
        }

        public bool SetFlipped(int instanceId, bool flipped)
        {
            PlacedObjectData placed = Find(instanceId);
            PlaceableData placeable = placed == null ? null : _design.GetPlaceable(placed.placeableId);
            if (placeable == null || (flipped && !placeable.canFlip)) return false;
            placed.flipped = flipped;
            _events?.RaiseBuildChanged(placed.area);
            return true;
        }

        /// <summary>Takes it out of the space. It goes back to the inventory if it came from there.</summary>
        public bool Remove(int instanceId)
        {
            PlacedObjectData placed = Find(instanceId);
            if (placed == null) return false;

            PlaceableData placeable = _design.GetPlaceable(placed.placeableId);
            if (placed.fromInventory && placeable != null && !_economy.Add(placeable.itemId, 1)) return false;

            _state.placed.Remove(placed);
            _events?.RaiseBuildChanged(placed.area);
            return true;
        }

        /// <summary>Puts down an area's starting layout the first time it's visited in a save. Free, and only once.</summary>
        public bool InitialiseArea(string area, IEnumerable<(string placeableId, float x, float y, bool flipped)> layout)
        {
            if (_state.initialisedAreas.Contains(area)) return false;
            _state.initialisedAreas.Add(area);
            foreach ((string placeableId, float x, float y, bool flipped) in layout)
            {
                PlaceableData placeable = _design.GetPlaceable(placeableId);
                if (placeable != null) Add(area, placeable, x, y, flipped, false);
            }
            _events?.RaiseBuildChanged(area);
            return true;
        }

        /// <summary>Seats placed in an area, for quests ("set out 4 seats") and, later, guests.</summary>
        public int CountSeats(string area)
        {
            int seats = 0;
            foreach (PlacedObjectData placed in GetPlaced(area))
                seats += _design.GetPlaceable(placed.placeableId)?.seats ?? 0;
            return seats;
        }

        private PlacedObjectData Add(string area, PlaceableData placeable, float x, float y, bool flipped, bool fromInventory)
        {
            var placed = new PlacedObjectData
            {
                instanceId = _state.nextPlacedId++,
                area = area,
                placeableId = placeable.id,
                x = x,
                y = y,
                flipped = flipped && placeable.canFlip,
                fromInventory = fromInventory
            };
            _state.placed.Add(placed);
            return placed;
        }
    }
}
