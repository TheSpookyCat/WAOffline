using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Bossa.Travellers.Ship;
using Bossa.Travellers.Utilityslot;
using Improbable.Collections;
using Improbable.Corelibrary.Math;
using Improbable.Math;
using Improbable.Worker;
using Improbable.Worker.Internal;
using WorldsAdriftRebornGameServer.DLLCommunication;
using WorldsAdriftRebornGameServer.Game;
using WorldsAdriftRebornGameServer.Game.Components;
using WorldsAdriftRebornGameServer.Game.Components.Update;
using WorldsAdriftRebornGameServer.Game.Components.Update.Handlers;
using WorldsAdriftRebornGameServer.Networking.Singleton;
using WorldsAdriftRebornGameServer.Networking.Wrapper;
using static WorldsAdriftRebornGameServer.DLLCommunication.EnetLayer;

namespace WorldsAdriftRebornGameServer
{
    using System;
    using System.Collections.Generic;


    internal class WorldsAdriftRebornGameServer
    {
        private static bool keepRunning = true;
        [PInvoke(typeof(EnetLayer.ENet_Poll_Callback))]
        private unsafe static void OnNewClientConnected(IntPtr peer )
        {
            ENetPeerHandle ePeer = new ENetPeerHandle(peer, new ENetHostHandle());
            if (!ePeer.IsInvalid)
            {
                Console.WriteLine("[info] got a connection.");
                PeerManager.Instance.playerState.Add(ePeer, new Dictionary<int, PlayerSyncStatus> { { 0, new PlayerSyncStatus() } });
            }
        }
        [PInvoke(typeof(EnetLayer.ENet_Poll_Callback))]
        private unsafe static void OnClientDisconnected(IntPtr peer )
        {
            ENetPeerHandle ePeer = new ENetPeerHandle(peer, new ENetHostHandle());
            if (!ePeer.IsInvalid)
            {
                Console.WriteLine("[info] a client disconnected.");
            }
        }

        private static readonly EnetLayer.ENet_Poll_Callback callbackC = new EnetLayer.ENet_Poll_Callback(OnNewClientConnected);
        private static readonly EnetLayer.ENet_Poll_Callback callbackD = new EnetLayer.ENet_Poll_Callback(OnClientDisconnected);
        private static readonly System.Collections.Generic.List<uint> authoritativeComponents = new System.Collections.Generic.List<uint>
        {
            // Authority is needed for a client to inject a writer into a behaviour
            // Writers are needed as a behaviour will self-disable unless all readers+writers are injected
            // Please document any additions!
            // Undocumented:
            8050, 8051, 6908, 1097, 1003, 1241, 1082, 
            
            1260, // SchematicsUnlearnerState | Used by InventoryVisualiser
            
            1145, // LogoutState | Used by LogoutBehaviour
            
            1093, // RespawnClientState | Used by RespawnVisualizer
            1072, // CharacterControlsData | Used by RespawnVisualizer
            
            1073, // ClientAuthoritativePlayerState | Used by ClientAuthoritativePlayerMovement
            190602, // TransformState
            
            1098, // RopeControlPoints | Used by RopeObserver
        };
        private static System.Collections.Generic.List<long> playerEntityIDs = new System.Collections.Generic.List<long>();

        private static long nextEntityId = 1;
        public static long NextEntityId
        {
            get
            {
                return nextEntityId++;
            }
        }

        private static void StartSpawningAllIslands( ENetPeerHandle peer, long playerId)
        {
            _ = TransformState_Handler.SpawnNearbyEntitiesAsync(peer, playerId, Vector3f.ZERO, 2000);
        }

        public static Dictionary<long, Dictionary<uint, object>> ComponentOverrideMap =
            new Dictionary<long, Dictionary<uint, object>>();

        public static void AddComponent<T>( long entityId, IComponentData<T> data ) where T : IComponentMetaclass
        {
            if (!ComponentOverrideMap.TryGetValue(entityId, out var datas))
            {
                datas = new Dictionary<uint, object>();
                ComponentOverrideMap[entityId] = datas;
            }

            datas[ComponentDatabase.MetaclassToId<T>()] = data;
        }
        
        public static void AddComponent( long entityId, uint componentId, object data ) 
        {
            if (!ComponentOverrideMap.TryGetValue(entityId, out var datas))
            {
                datas = new Dictionary<uint, object>();
                ComponentOverrideMap[entityId] = datas;
            }

            datas[componentId] = data;
        }
        
        static unsafe void Main( string[] args )
        {
            Console.CancelKeyPress += delegate ( object? sender, ConsoleCancelEventArgs e )
            {
                keepRunning = false;
            };

            OfflineReplicationRegistry.RegisterEntities();
            var hack = new FuelGaugeState.Data(100f, 100f);  // required or ComponentDatabase won't be initialized properly
            Console.WriteLine($"INFO - Component Handles Registered: {ComponentUpdateManager.Instance != null} {ComponentsManager.Instance != null} | {ComponentDatabase.MetaclassMap.Count} Components");
            
            if (EnetLayer.ENet_Initialize() < 0)
            {
                Console.WriteLine("[error] failed to initialize ENet.");
                return;
            }

            Console.WriteLine("[info] successfully initialized ENet.");
            ENetHostHandle server = EnetLayer.ENet_Create_Host(7777, 1, 5, 0, 0);

            if (server.IsInvalid)
            {
                Console.WriteLine("[error] failed to create host and listen on network interface.");

                EnetLayer.ENet_Deinitialize(new IntPtr(0));
                return;
            }

            Console.WriteLine("[info] successfully initialized networking, now waiting for connections and data.");
            PeerManager.Instance.SetENetHostHandle(server);

            var islandAssets = WorldMapData.Instance.Islands
                                           .Select(i => i.Island.Replace(".json", "") + "@Island")
                                           .Distinct()
                                           .ToList();
            
            var syncSteps = new System.Collections.Generic.List<SyncStep>();

            syncSteps.Add(new SyncStep(
                GameState.NextStateRequirement.ASSET_LOADED_RESPONSE,
                o => SendOPHelper.SendAssetLoadRequestOP(
                    (ENetPeerHandle)o, "notNeeded?", "GlobalEntity", "notNeeded?")
            ));

            syncSteps.Add(new SyncStep(
                GameState.NextStateRequirement.ASSET_LOADED_RESPONSE,
                o => SendOPHelper.SendAssetLoadRequestOP(
                    (ENetPeerHandle)o, "notNeeded?", "WallSegment", "notNeeded?")
            ));
            
            syncSteps.Add(new SyncStep(
                GameState.NextStateRequirement.ASSET_LOADED_RESPONSE,
                o => SendOPHelper.SendAssetLoadRequestOP(
                    (ENetPeerHandle)o, "notNeeded?", "WeatherCell", "notNeeded?")
            ));

            syncSteps.Add(new SyncStep(
                GameState.NextStateRequirement.ASSET_LOADED_RESPONSE,
                o => SendOPHelper.SendAssetLoadRequestOP(
                    (ENetPeerHandle)o, "notNeeded?", "Traveller", "Player")
            ));

            foreach (var island in islandAssets)
            {
                syncSteps.Add(new SyncStep(
                    GameState.NextStateRequirement.ASSET_LOADED_RESPONSE,
                    o =>
                    {
                        Console.WriteLine($"[info] requesting asset load for {island}");
                        SendOPHelper.SendAssetLoadRequestOP(
                            (ENetPeerHandle)o, "notNeeded?", island, "notNeeded?");
                    }));
            }
            // Keep the current 0,0,0 island for spawning simplicity
            syncSteps.Add(new SyncStep(
                GameState.NextStateRequirement.ADDED_ENTITY_RESPONSE,
                o =>
                {
                    const string rootIsland = "949069116@Island";

                    Console.WriteLine("[success] island asset loaded. requesting loading of island...");

                    if (!SendOPHelper.SendAddEntityOP(
                            (ENetPeerHandle)o,
                            NextEntityId,
                            rootIsland,
                            "notNeeded?"))
                    {
                        Console.WriteLine("[error] failed to serialize and queue AddEntityOp.");
                    }
                }
            ));
            
            syncSteps.Add(new SyncStep(
                GameState.NextStateRequirement.ADDED_ENTITY_RESPONSE,
                o =>
                {
                    Console.WriteLine("[info] client ack'ed island spawn. requesting to spawn player...");

                    var playerId = NextEntityId;
                    playerEntityIDs.Add(playerId);
                    
                    // AddComponent(playerId, new TransformState.Data(TransformState_Handler.CreateFixedVector(613.58325f, 141.59912f, -36.614258f),
                    //     new Quaternion32(1023),
                    //     null,
                    //     Vector3d.ZERO, 
                    //     Vector3f.ZERO, 
                    //     Vector3f.ZERO, 
                    //     false, 1000
                    // ));

                    if (!SendOPHelper.SendAddEntityOP(
                            (ENetPeerHandle)o,
                            playerEntityIDs.Last(),
                            "Traveller",
                            "Player"))
                    {
                        Console.WriteLine("[error] failed to serialize and queue AddEntityOp.");
                        return;
                    }

                    Console.WriteLine($"INFO - Player is spawning as entity {playerId}");
                    // spawn remaining islands
                    StartSpawningAllIslands((ENetPeerHandle)o, playerId);
                }
            ));
            GameState.Instance.WorldState[0] = syncSteps;

            while (keepRunning)
            {
                EnetLayer.ENetPacket_Wrapper* packet = EnetLayer.ENet_Poll(server, 25, Marshal.GetFunctionPointerForDelegate(callbackC), Marshal.GetFunctionPointerForDelegate(callbackD));
                if(packet != null)
                {
                    // work on packets that are relevant to progress in sync state
                    foreach (KeyValuePair<ENetPeerHandle, Dictionary<int, PlayerSyncStatus>> keyValuePair in PeerManager.Instance.playerState)
                    {
                        int currentChunkIndex = 0;
                        int currentPlayerSyncIndex = PeerManager.Instance.playerState[keyValuePair.Key][currentChunkIndex].SyncStepPointer;

                        if (currentPlayerSyncIndex == GameState.Instance.WorldState[currentChunkIndex].Count - 1)
                        {
                            // this player is synced
                            continue;
                        }

                        GameState.NextStateRequirement nextStateRequirement = GameState.Instance.WorldState[currentChunkIndex][currentPlayerSyncIndex].NextStateRequirement;

                        if(packet->Channel == (int)EnetLayer.ENetChannel.ASSET_LOAD_REQUEST_OP && nextStateRequirement == GameState.NextStateRequirement.ASSET_LOADED_RESPONSE)
                        {
                            // for now set it for every client, but we need to distinguish them by their userData field
                            PeerManager.Instance.playerState[keyValuePair.Key][currentChunkIndex].SyncStepPointer++;
                        }
                        else if(packet->Channel == (int)EnetLayer.ENetChannel.ADD_ENTITY_OP && nextStateRequirement == GameState.NextStateRequirement.ADDED_ENTITY_RESPONSE)
                        {
                            PeerManager.Instance.playerState[keyValuePair.Key][currentChunkIndex].SyncStepPointer++;
                        }
                    }

                    // work on packets that are not relevant for progress of sync state but need processing for any player
                    foreach (KeyValuePair<ENetPeerHandle, Dictionary<int, PlayerSyncStatus>> keyValuePair in PeerManager.Instance.playerState)
                    {
                        if (packet->Channel == (int)EnetLayer.ENetChannel.SEND_COMPONENT_INTEREST)
                        {
                            long entityId = 0;
                            uint interestCount = 0;
                            Structs.Structs.InterestOverride* interests = (Structs.Structs.InterestOverride*)new IntPtr(0);

                            if (EnetLayer.PB_EXP_SendComponentInterest_Deserialize(packet->Data, (int)packet->DataLength, &entityId, &interests, &interestCount))
                            {
                                Console.WriteLine("[info] game requests components for entity id: " + entityId);

                                if(playerEntityIDs.Contains(entityId) && !PeerManager.Instance.clientSetupState.Contains(keyValuePair.Key))
                                {
                                    // a player entity requests components for the first time, we need to setup a few things to make him work properly
                                    // some of this might not be needd anymore in the future once we sorted out a few things.
                                    //
                                    // we can make use of the fact that the game requests components for players in two stages, where the second one will terminate the loading screen of the client.
                                    // the second stage needs a few components setup properly, for this we need to inject one component and call auth changed for a few others once.

                                    // some components are needed in the first stage and need to be injected.
                                    // we also need PilotState since schematics for glider where added, as the game nullrefs in PlayerExternalDataVisualizer.IsDriving() now (1109)
                                    System.Collections.Generic.List<Structs.Structs.InterestOverride> injectedEarly = new System.Collections.Generic.List<Structs.Structs.InterestOverride> { new Structs.Structs.InterestOverride(1109, 1) };

                                    if (!SendOPHelper.SendAddComponentOp(keyValuePair.Key, entityId, injectedEarly, true))
                                    {
                                        continue;
                                    }

                                    // then send what the game requested
                                    if (!SendOPHelper.SendAddComponentOp(keyValuePair.Key, entityId, interests, interestCount, true))
                                    {
                                        continue;
                                    }

                                    // for some reason the game does not always request component 1080 (SchematicsLearnerGSimState), but its reader is required in InventoryVisualiser
                                    System.Collections.Generic.List<Structs.Structs.InterestOverride> injected = new System.Collections.Generic.List<Structs.Structs.InterestOverride> { new Structs.Structs.InterestOverride(1080, 1) };
                                    // also inject other required components for the inventory
                                    injected.AddRange(authoritativeComponents.Select(p => new Structs.Structs.InterestOverride(p, 1)));

                                    if (!SendOPHelper.SendAddComponentOp(keyValuePair.Key, entityId, injected, true))
                                    {
                                        continue;
                                    }

                                    // now send auth change
                                    if(!SendOPHelper.SendAuthorityChangeOp(keyValuePair.Key, entityId, authoritativeComponents))
                                    {
                                        continue;
                                    }

                                    // now add player to clientSetupState
                                    PeerManager.Instance.clientSetupState.Add(keyValuePair.Key);
                                }
                                else
                                {
                                    // player already setup or another entity requested components, so just process them
                                    if (!SendOPHelper.SendAddComponentOp(keyValuePair.Key, entityId, interests, interestCount, true))
                                    {
                                        continue;
                                    }
                                }
                            }
                            else
                            {
                                Console.WriteLine("[error] failed to deserialize ComponentInterest message from game.");
                            }
                        }
                        else if(packet->Channel == (int)EnetLayer.ENetChannel.COMPONENT_UPDATE_OP)
                        {
                            long entityId = 0;
                            uint updateCount = 0;
                            Structs.Structs.ComponentUpdateOp* update = (Structs.Structs.ComponentUpdateOp*)new IntPtr(0);

                            if (EnetLayer.PB_EXP_ComponentUpdateOp_Deserialize(packet->Data, (int)packet->DataLength, &entityId, &update, &updateCount) && updateCount > 0)
                            {
                                // Console.WriteLine("[info] game requests " + updateCount + " ComponentUpdate's for entity id " + entityId);

                                for(int i = 0; i < updateCount; i++)
                                {
                                    ComponentUpdateManager.Instance.HandleComponentUpdate(keyValuePair.Key, entityId, update[i].ComponentId, update[i].ComponentData, update[i].DataLength);
                                }
                            }
                            else
                            {
                                Console.WriteLine("[error] failed to deserialize ComponentUpdate message from game, or empty message.");
                            }
                        }
                    }

                    EnetLayer.ENet_Destroy_Packet(new IntPtr(packet));
                }

                // dont wait for GetOplist and then for the Dispatch call as we are the ones who would dispatch the work anyways.
                // sync up players
                foreach (KeyValuePair<ENetPeerHandle, Dictionary<int, PlayerSyncStatus>> keyValuePair in PeerManager.Instance.playerState)
                {
                    int currentChunkIndex = 0;
                    PlayerSyncStatus pStatus = keyValuePair.Value[currentChunkIndex];
                    SyncStep step = GameState.Instance.WorldState[currentChunkIndex][pStatus.SyncStepPointer];

                    if (!pStatus.Performed)
                    {
                        step.Step(keyValuePair.Key);
                        pStatus.Performed = true;
                    }
                }
            }

            server.Dispose();

            Console.WriteLine("[info] shutting down.");
        }
    }
}
