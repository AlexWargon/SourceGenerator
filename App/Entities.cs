using System;
using App;

namespace Wargon.ezs {
    public struct Entities {
        public void Each<T1, T2, T3>(Action<T1, T2, T3> action) { }
        public void Each<T1, T2>(Action<T1, T2> action) { }

        public Entities Without<NT>() {
            return this;
        }

        public Entities Without<NT1, NT2>() {
            return this;
        }
    }

    public interface IFilter { }

    public class Pool<T> {
        public T[] items;
    }

    public struct With<T> : IFilter { }

    public struct With<T1, T2> : IFilter { }

    public struct With<T1, T2, T3> : IFilter { }

    public struct With<T1, T2, T3, T4> : IFilter { }

    public struct EntityQuery<TWith> where TWith : struct, IFilter {
        public int[] entities;
        public int Count;

        public EntityQuery(World world) {
            entities = new int[128];
            Count = 0;
        }

        public EntityQuery<TWith> Without<T>() {
            return this;
        }
    }
}