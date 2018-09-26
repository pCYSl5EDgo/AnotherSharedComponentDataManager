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
            World.Active = world = new World("優秀さを証明するためのテストワールド");
            var manager = world.CreateManager<EntityManager>();
            InitializeEntities(manager);
            world.CreateManager(typeof(ManyChangeSharedValueSystem), ValueArray);
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
            var archetype = manager.CreateArchetype(ComponentType.Create<TEST0>(), ComponentType.Create<TEST1>(), ComponentType.Create<TEST2>(), ComponentType.Create<TEST3>(), ComponentType.Create<TEST4>(), ComponentType.Create<TEST5>(), ComponentType.Create<TEST6>(), ComponentType.Create<TEST7>(), ComponentType.Create<TEST8>(), ComponentType.Create<TEST9>(), ComponentType.Create<TEST10>(), ComponentType.Create<TEST11>(), ComponentType.Create<TEST12>(), ComponentType.Create<TEST13>(), ComponentType.Create<TEST14>(), ComponentType.Create<TEST15>());
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
                    TEST3 t3 = default;
                    TEST4 t4 = default;
                    TEST5 t5 = default;
                    TEST6 t6 = default;
                    TEST7 t7 = default;
                    TEST8 t8 = default;
                    TEST9 t9 = default;
                    TEST10 t10 = default;
                    TEST11 t11 = default;
                    TEST12 t12 = default;
                    TEST13 t13 = default;
                    TEST14 t14 = default;
                    TEST15 t15 = default;
                    t0.Value =
                    t1.Value =
                    t2.Value =
                    t3.Value =
                    t4.Value =
                    t5.Value =
                    t6.Value =
                    t7.Value =
                    t8.Value =
                    t9.Value =
                    t10.Value =
                    t11.Value =
                    t12.Value =
                    t13.Value =
                    t14.Value =
                    t15.Value =
                    ValueArray[1];
                    manager.SetSharedComponentData(entities[0], t0);
                    manager.SetSharedComponentData(entities[0], t1);
                    manager.SetSharedComponentData(entities[0], t2);
                    manager.SetSharedComponentData(entities[0], t3);
                    manager.SetSharedComponentData(entities[0], t4);
                    manager.SetSharedComponentData(entities[0], t5);
                    manager.SetSharedComponentData(entities[0], t6);
                    manager.SetSharedComponentData(entities[0], t7);
                    manager.SetSharedComponentData(entities[0], t8);
                    manager.SetSharedComponentData(entities[0], t9);
                    manager.SetSharedComponentData(entities[0], t10);
                    manager.SetSharedComponentData(entities[0], t11);
                    manager.SetSharedComponentData(entities[0], t12);
                    manager.SetSharedComponentData(entities[0], t13);
                    manager.SetSharedComponentData(entities[0], t14);
                    manager.SetSharedComponentData(entities[0], t15);
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
                        if (j == ValueArray.Length)
                            j = 0;
                        t3.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t4.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t5.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t6.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t7.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t8.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t9.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t10.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t11.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t12.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t13.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t14.Value = ValueArray[j++];
                        if (j == ValueArray.Length)
                            j = 0;
                        t15.Value = ValueArray[j++];
                        manager.SetSharedComponentData(entities[i], t0);
                        manager.SetSharedComponentData(entities[i], t1);
                        manager.SetSharedComponentData(entities[i], t2);
                        manager.SetSharedComponentData(entities[i], t3);
                        manager.SetSharedComponentData(entities[i], t4);
                        manager.SetSharedComponentData(entities[i], t5);
                        manager.SetSharedComponentData(entities[i], t6);
                        manager.SetSharedComponentData(entities[i], t7);
                        manager.SetSharedComponentData(entities[i], t8);
                        manager.SetSharedComponentData(entities[i], t9);
                        manager.SetSharedComponentData(entities[i], t10);
                        manager.SetSharedComponentData(entities[i], t11);
                        manager.SetSharedComponentData(entities[i], t12);
                        manager.SetSharedComponentData(entities[i], t13);
                        manager.SetSharedComponentData(entities[i], t14);
                        manager.SetSharedComponentData(entities[i], t15);
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