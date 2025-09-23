using UnityEngine;

namespace GridCellTypes
{
    public enum Direction { North, East, South, West }

    public struct CellData
    {
        //any object - gameobject, mono script, so
        public Object occupant;

        public bool Occupied
        {
            get { return occupant != null; }
        }
    }   

    public struct Neighbor
    {
        public Vector2Int coord;
        public Direction dir;
        public bool occupied;

        public Neighbor(Vector2Int c, Direction d, bool occ)
        {
            coord = c;
            dir = d;
            occupied = occ;
        }
    }
}
