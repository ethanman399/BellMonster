using System.Collections;
using System.Diagnostics;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using System.Threading;

namespace BellMonster {

    // You may be wondering, how does the Bell Monster know it is from class BellMonsterAI?
    // Well, we give it a reference to to this class in the Unity project where we make the asset bundle.
    // Asset bundles cannot contain scripts, so our script lives here. It is important to get the
    // reference right, or else it will not find this file. See the guide for more information.

    class BellMonsterAI : EnemyAI
    {
        // We set these in our Asset Bundle, so we can disable warning CS0649:
        // Field 'field' is never assigned to, and will always have its default value 'value'
        #pragma warning disable 0649
        #pragma warning restore 0649
        private System.Random? bellPitchRandom;

        enum State {
            SearchingForPlayer,
            ChasingPlayer,
        }

        [Conditional("DEBUG")]
        void LogIfDebugBuild(string text) {
            Plugin.Logger.LogInfo(text);
        }

        public override void Start()
        {
            base.Start();
            LogIfDebugBuild("Bell Monster Spawned");
            SwitchToBehaviourClientRpc((int)State.SearchingForPlayer);
            // We make the enemy start searching. This will make it start wandering around.
            bellPitchRandom = new System.Random((int)(base.transform.position.x + base.transform.position.z));

            StartSearch(transform.position);
            StartCoroutine(PlayStep());
        }

        public override void Update() {
            base.Update();
            if(isEnemyDead){
                return;
            }
        }

        public override void DoAIInterval() {
            
            base.DoAIInterval();
            if (isEnemyDead || StartOfRound.Instance.allPlayersDead) {
                return;
            };

            switch(currentBehaviourStateIndex) {
                case (int)State.SearchingForPlayer:
                    agent.speed = 1f;
                    if (FoundClosestPlayerInRange(15f, 10f)){
                        LogIfDebugBuild("Start Target Player");
                        StopSearch(currentSearch);
                        SwitchToBehaviourClientRpc((int)State.ChasingPlayer);
                    }
                    break;

                case (int)State.ChasingPlayer:
                    agent.speed = 3f;
                    // Keep targeting closest player, unless they are over 10 units away and we can't see them.
                    if (!TargetClosestPlayerInAnyCase() || (Vector3.Distance(transform.position, targetPlayer.transform.position) > 20 && !CheckLineOfSightForPosition(targetPlayer.transform.position))){
                        LogIfDebugBuild("Stop Target Player");
                        StartSearch(transform.position);
                        SwitchToBehaviourClientRpc((int)State.SearchingForPlayer);
                        return;
                    }
                    SetDestinationToPosition(targetPlayer.transform.position);
                    targetPlayer.JumpToFearLevel(0.5f);
                    break;
                    
                default:
                    LogIfDebugBuild("This Behavior State doesn't exist!");
                    break;
            }
        }

        //coroutine for playing step noises
        public IEnumerator PlayStep()
        {
            while (!isEnemyDead)
            {
                if (currentBehaviourStateIndex == (int)State.SearchingForPlayer)
                {
                    //While searching for player, mimic belldrop sfx of handbell item (vanilla LC)
                    Plugin.Logger.LogInfo("Playing bellStep sound (Searching)");
                    creatureSFX.pitch = 1f;
                    switch (bellPitchRandom?.Next(0, 7))
                    {
                        case 1:
                            creatureSFX.pitch *= Mathf.Pow(1.05946f, 3f);
                            break;
                        case 2:
                            creatureSFX.pitch *= Mathf.Pow(1.05946f, 5f);
                            break;
                        case 3:
                            creatureSFX.pitch /= Mathf.Pow(1.05946f, 3f);
                            break;
                        case 4:
                            creatureSFX.pitch /= Mathf.Pow(1.05946f, 5f);
                            break;
                        case 5:
                            creatureSFX.pitch /= Mathf.Pow(1.05946f, 7f);
                            break;
                        case 6:
                            creatureSFX.pitch /= Mathf.Pow(1.05946f, 10f);
                            break;
                    }

                    creatureSFX.PlayOneShot(Plugin.bellStep);
                    WalkieTalkie.TransmitOneShotAudio(creatureSFX, Plugin.bellStep);

                    //Play randomly every 3f-4f
                    yield return new WaitForSeconds(UnityEngine.Random.Range(3f, 4f));
                    yield return null;
                }

                //While chasing player, play distorted creepy version of belldrop sfx
                if (currentBehaviourStateIndex == (int)State.ChasingPlayer)
                {
                    Plugin.Logger.LogInfo("Playing bellStep sound (Chasing)");
                    //Plays at a much lower pitch and faster pace compared to searching
                    creatureSFX.pitch = UnityEngine.Random.Range(0.2f, 0.4f);
                    creatureSFX.PlayOneShot(Plugin.bellStep);
                    WalkieTalkie.TransmitOneShotAudio(creatureSFX, Plugin.bellStep);
                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.4f, 0.8f));
                    yield return null;
                }
            }
        }

        bool FoundClosestPlayerInRange(float range, float senseRange) {
            TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: true);
            if(targetPlayer == null){
                // Couldn't see a player, so we check if a player is in sensing distance instead
                TargetClosestPlayer(bufferDistance: 1.5f, requireLineOfSight: false);
                range = senseRange;
            }
            return targetPlayer != null && Vector3.Distance(transform.position, targetPlayer.transform.position) < range;
        }
        
        bool TargetClosestPlayerInAnyCase() {
            mostOptimalDistance = 2000f;
            targetPlayer = null;
            for (int i = 0; i < StartOfRound.Instance.connectedPlayersAmount + 1; i++)
            {
                tempDist = Vector3.Distance(transform.position, StartOfRound.Instance.allPlayerScripts[i].transform.position);
                if (tempDist < mostOptimalDistance)
                {
                    mostOptimalDistance = tempDist;
                    targetPlayer = StartOfRound.Instance.allPlayerScripts[i];
                }
            }
            if(targetPlayer == null) return false;
            return true;
        }

        public override void OnCollideWithPlayer(Collider other) {
            PlayerControllerB playerControllerB = MeetsStandardPlayerCollisionConditions(other);
			if (playerControllerB != null)
			{
				playerControllerB.DamagePlayer(90, hasDamageSFX: true, callRPC: true, CauseOfDeath.Mauling);
				playerControllerB.JumpToFearLevel(1f);
			}
        }
    }
}