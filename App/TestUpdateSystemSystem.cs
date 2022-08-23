using System;
using App;
using Wargon.ezs;

public partial class TestUpdateSystemSystem : UpdateSystem {

    public override void Update() {
        entities.Each((int a1, float b1, char c1) => {
            entities.Each((int a2, float b2, char c2) => {


            });
            entities.Each((int a, float b, char c) => {
                    
            
            });
        });
        entities.Each((int z, float b, char c) => {
            z += 2;

        });
    }
}

namespace App{
    


    public struct EntityType<T1, T2, T3> { }

    public class World {
        public Pool<T> GetPool<T>() {
            return new();
        }
    }

    public struct Inactive { }

    public struct Health {
        public int value;
    }

    public struct Heal {
        public int value;
    }

    public struct PlayerTag { }
}