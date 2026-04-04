using System;
using UnityEngine;

namespace QuickFreeplay
{
    [Serializable]
    public struct PlacedBlockData
    {
        public int PrefabID;
        public Vector3 Position;
        public Vector3 LocalScale;
        public Quaternion Rotation;
    }
}
