using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace この名前に意味は特にないテスト
{
    public sealed class Test2Manager : MonoBehaviour
    {
        void Start()
        {
#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
            World.DisposeAllWorlds();
#endif
            var worlds = new World[1];
            ref var world = ref worlds[0];
            World.Active = world = new World("キングテレサ姫可愛いテストワールド");
            var manager = world.CreateManager<EntityManager>();
            InitializeEntities(manager);
            world.CreateManager(typeof(TripleChangeSharedValueSystem), ValueArray);
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop(worlds);
        }
        private static readonly ulong[] ValueArray = new ulong[]{
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            8,
            9,
            10,
            11,
            12,
            14,
            ulong.MaxValue,
        };
        private static void InitializeEntities(EntityManager manager)
        {
            var entities = new NativeArray<Entity>(1 << 10, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var archetype = manager.CreateArchetype(ComponentType.Create<TEST0>(), ComponentType.Create<TEST1>(), ComponentType.Create<TEST2>());
            try
            {
                unsafe
                {
                    var skipFirstEntityArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<Entity>(((Entity*)NativeArrayUnsafeUtility.GetUnsafePtr(entities)) + 1, entities.Length - 1, Allocator.None);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref skipFirstEntityArray, NativeArrayUnsafeUtility.GetAtomicSafetyHandle(entities));
#endif
                    entities[0] = manager.CreateEntity(archetype);
                    manager.Instantiate(entities[0], skipFirstEntityArray);
                    TEST0 t0 = default;
                    TEST1 t1 = default;
                    TEST2 t2 = default;
                    t0.Value = ValueArray[1];
                    t1.Value = ValueArray[1];
                    t2.Value = ValueArray[1];
                    manager.SetSharedComponentData(entities[0], t0);
                    manager.SetSharedComponentData(entities[0], t1);
                    manager.SetSharedComponentData(entities[0], t2);
                    for (int i = 0, j = 0; i < entities.Length; ++i)
                    {
                        if (j == ValueArray.Length)
                            j = 0;
                        t0.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t1.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t2.Value = ValueArray[j++];
                        manager.SetSharedComponentData(entities[i], t0);
                        manager.SetSharedComponentData(entities[i], t1);
                        manager.SetSharedComponentData(entities[i], t2);
                    }
                }
            }
            finally
            {
                entities.Dispose();
            }
        }


        void OnDestroy()
        {
            World.DisposeAllWorlds();
        }
    }
}