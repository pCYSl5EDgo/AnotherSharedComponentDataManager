﻿using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace おう考えてやるからあくしろよテスト
{
    public sealed class Manager : MonoBehaviour
    {
        void Start()
        {
#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
            World.DisposeAllWorlds();
#endif
            var worlds = new World[1];
            ref var world = ref worlds[0];
            World.Active = world = new World("ECS完全に理解したテストワールド");
            var manager = world.CreateManager<EntityManager>();
            InitializeEntities(manager);
            world.CreateManager(typeof(ChangeSharedValueSystem), ValueArray);
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
            ulong.MaxValue,
        };
        private static void InitializeEntities(EntityManager manager)
        {
            var entities = new NativeArray<Entity>(1 << 10, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var archetype = manager.CreateArchetype(ComponentType.Create<TEST>());
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
                    TEST t = default;
                    t.Value = ValueArray[0];
                    manager.SetSharedComponentData(entities[0], t);
                    for (int i = 0, j = 0; i < entities.Length; ++i, ++j)
                    {
                        if (j == ValueArray.Length)
                            j = 0;
                        t.Value = ValueArray[j];
                        manager.SetSharedComponentData(entities[i], t);
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