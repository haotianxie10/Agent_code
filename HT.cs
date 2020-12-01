using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class HT : Agent {
//    static bool DEBUG_AI = true;
//    struct SPMemory {
//        public SpawnPoint   sp;
//        public PickUp.eType pickUp;
//
//        public SPMemory(SpawnPoint eSP) {
//            sp = eSP;
//        }
//    }
    private static Dictionary<PickUp.eType, List<SpawnPoint>>  MEM;

    [Header("Inscribed JBAgent_Utility")]
    public float            targetProximity = 1f;
    public bool             leadTargets = true;


    [Header("Dynamic JBAgent_Utility")]
    public eStrat           aiStrat;
    public float            aiU;
    public List<UtilRec>    aiUtilRecs;
    [Tooltip("x=none y=health z=ammo")]
    public Vector3          spawnPointKnowledge = Vector3.zero;
    [SerializeField]
    private Vector3         navMeshTargetLoc = Vector3.one * 9999;
    private SpawnPoint      sPoint;
    private bool            inited = false;

    void Init()
    {
        // I do this because any settings I made in the Inspector wouldn't be copied when I
        //   use GameObject.AddComponent() in ArenaManager
        SetAIUtilRecs();

        // Get a list of where all the item SpawnPoints are
        if (MEM == null)
        {
            MEM = new Dictionary<PickUp.eType, List<SpawnPoint>>();
            List<SpawnPoint> sPoints = SpawnPoint.GET_SPAWN_POINTS(SpawnPoint.eType.item);
            MEM.Add(PickUp.eType.none, new List<SpawnPoint>());
            foreach (SpawnPoint sp in sPoints)
            {
                MEM[PickUp.eType.none].Add(sp); // I'm using none is for unknown type
            }
        }

        inited = true;
    }

    #region SetAIUtilRecs
    // This does ALL the settings of values on aiUtilRecs because anything I set in the Inspector
    //   is not copied when I use GameObject.AddComponent() in ArenaManager
    void SetAIUtilRecs() {
        aiUtilRecs = new List<UtilRec>();
        UtilRec ur;

        // Explore
        ur = new UtilRec();
        ur.stratType = eStrat.explore;
        ur.enabled = true;
        ur.utilMinMax = new Vector2( 0.0f, 0.4f );
        ur.curveFunc = x => x;
        ur.strat = new JBS_Explore(ur);
        aiUtilRecs.Add(ur);

        // Wander
        ur = new UtilRec();
        ur.stratType = eStrat.wander;
        ur.enabled = true;
        ur.utilMinMax = new Vector2( 0.3f, 0.3f );
        ur.curveFunc = x => x;
        ur.strat = new JBS_Wander(ur);
        aiUtilRecs.Add(ur);

        // I've removed the rest of these for you to figure out yourself. – JGB

    }
    #endregion


    #region OnDrawGizmos
    public override void OnDrawGizmos() {
        base.OnDrawGizmos();

        if (Application.isPlaying) {
            Vector3 pV = transform.up * 0.5f;

            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position+pV, navMeshTargetLoc+pV);

            if (closestEnemy != null) {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(closestEnemy.pos+pV, 1f);
                Gizmos.DrawLine(headTrans.position, headTrans.position+headTrans.forward*40);
            }

        }
    }
    #endregion

    Vector3 toClosestEnemy;
    Agent closestEnemy;

    /// <summary>
    /// The main method that you should override in your Agent subclass is AIUpdate().
    /// </summary>
    /// <param name="inputs"></param>
    public override void AIUpdate(List<SensoryInput> inputs)
    {
        base.AIUpdate(inputs);
        // THE FIRST LINE OF YOUR AIUpdate() override MUST be: base.AIUpdate(inputs);
        // AIUpdate copies inputs into sensed

        if (!inited) Init();

        // AIUpdate copies inputs into sensed
        SensedObject sawSomeone = null;
        toClosestEnemy = Vector3.one * 1000;
        closestEnemy = null;
        foreach (SensoryInput si in sensed) {
            switch (si.sense) { 
                case SensoryInput.eSense.vision:
                    if (si.type == eSensedObjectType.enemy) {
                        sawSomeone = si.obj;
                        // Check to see whether the Enemy is within the firing arc
                        // The dot product of two vectors is the magnitude of A * the magnitude of B * cos(the angle between them)
                        Vector3 toEnemy = si.pos - pos;
                        if (toEnemy.magnitude < toClosestEnemy.magnitude) {
                            toClosestEnemy = toEnemy;
                            closestEnemy = si.obj as Agent;
                        }
//                        float dotProduct = Vector3.Dot(headTrans.forward, toEnemy.normalized);
//                        float theta = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
//                        if (theta <= ArenaManager.AGENT_SETTINGS.bulletAimVarianceDeg) {
//                            if (ammo > 0) {
//                                Fire();
//                            }
//                        }
                    }
                    break;
            }

            // Regardless of the sense type, I want to know if it was a PickUp and what it was
            if (si.type == eSensedObjectType.item) {
                PickUp.eType puType = (si.obj as PickUp).puType;
                // Check the position of this item relative to the position of known item SpawnPoints
                if (MEM[PickUp.eType.none].Count > 0) {
                    SpawnPoint foundSP = null;
                    foreach (SpawnPoint sp in MEM[PickUp.eType.none]) {
                        if ( (sp.pos-si.obj.pos).magnitude < 0.1f ) { // We found it!
                            foundSP = sp;
                            break;
                        }
                    }
                    if (foundSP != null) {
                        MEM[PickUp.eType.none].Remove(foundSP);
                        if (!MEM.ContainsKey(puType)) {
                            MEM.Add(puType, new List<SpawnPoint>());
                        }
                        MEM[puType].Add(foundSP);
                    } else {
                        // No big deal, this just means that we already know about the SpawnPoint at that loc.
                    }
                }
            }

            if (si.type == eSensedObjectType.enemy) {
                // tracker.Track(si);
            }
        }
        if (sawSomeone == null) {
            LookCenter();
//            leadObj = null;
        } else {



            // Turn head to track enemies
            // How far is the enemy from current headTrans.forward
            float dotProdToClosest = Vector3.Dot(toClosestEnemy.normalized, headTrans.forward);
            // Account for the NaN errors that happen if Mathf.Acos(x) is called for x outside of -1 < x < 1
            dotProdToClosest = Mathf.Clamp(dotProdToClosest, -1, 1);
            float angToClosest = Mathf.Acos( dotProdToClosest ) * Mathf.Rad2Deg;
            // This shouldn't be needed because of the Clamp above
//            if (Mathf.Approximately(dotProdToClosest,1)) {
//                angToClosest = 0;
//            } else if (Mathf.Approximately(dotProdToClosest,-1)) {
//                angToClosest = 180;
//            } else {
//                angToClosest = Mathf.Acos( dotProdToClosest ) * Mathf.Rad2Deg;
//            }
//            // The following is here because the Acos of 1 is NaN.
//            if (float.IsNaN(angToClosest)) {
//                angToClosest = 0;
//            }
            // A dot product of toEnemy and transform.right will tell you whether to look left or right
            float posNeg = (Vector3.Dot(toClosestEnemy.normalized, headTrans.right) < 0) ? -1 : 1;
            angToClosest *= posNeg;
//            print("AngleToClosest: "+angToClosest);
            LookTheta(angToClosest);
            if (angToClosest <= ArenaManager.AGENT_SETTINGS.bulletAimVarianceDeg) {
                if (ammo > 0) {
                    Fire();
                }
            }
        }



        // Utility AI – Iterate through all the aiUtilRecs and find the one with the highest utility
        float maxUtil = -1;
        UtilRec uRecBest = null;
        foreach (UtilRec ur in aiUtilRecs)
        {
            if (ur.strat.Utility(this) > maxUtil)
            {
                uRecBest = ur;
                maxUtil = ur.u;
            }
        }
        aiU = maxUtil;
        aiStrat = uRecBest.stratType;

        if (uRecBest.dest != nmAgent.destination && health > 0)
        {
            nmAgent.SetDestination(uRecBest.dest);
            navMeshTargetLoc = uRecBest.dest;
        }

        // report on knowledge
        spawnPointKnowledge.x = MEM.ContainsKey(PickUp.eType.none) ? MEM[PickUp.eType.none].Count : 0;
        spawnPointKnowledge.y = MEM.ContainsKey(PickUp.eType.health) ? MEM[PickUp.eType.health].Count : 0;
        spawnPointKnowledge.z = MEM.ContainsKey(PickUp.eType.ammo) ? MEM[PickUp.eType.ammo].Count : 0;





        if (health > 0) {
            nmAgent.SetDestination(nmAgent.destination);
        }
    }




    public List<SpawnPoint> GetSPList(PickUp.eType puType = PickUp.eType.none) {
        List<SpawnPoint> spL = new List<SpawnPoint>();
        foreach (KeyValuePair<PickUp.eType, List<SpawnPoint>> kvp in MEM) {
            if (puType == PickUp.eType.none || puType == kvp.Key) {
                spL.AddRange( kvp.Value );
            }
        }
        return spL;
    }

    #region SpawnPoint_Management
    // Find the nearest SpawnPoint in the List spL
    // Pass in an ignoreSP to ignore one SpawnPoint (covers the case when we've gotten there, and there is no item)
    public SpawnPoint ClosestSPFromList(List<SpawnPoint> spL, SpawnPoint ignoreSP=null) {
        SpawnPoint foundSP = null;
        float dist = float.MaxValue;
        foreach (SpawnPoint sp in spL) {
            if (sp == ignoreSP) {
                continue; // Skip the one we're supposed to ignore
            }
            if ((sp.pos-pos).magnitude < dist) {
                foundSP = sp;
                dist = (sp.pos-pos).magnitude;
            }
        }
        return foundSP; // This could very well return null. Be ready for that!
    }

    // A puType of none will accept any item SpawnPoint
    public SpawnPoint RandomSP(PickUp.eType puType = PickUp.eType.none) {
        List<SpawnPoint> spL = GetSPList(puType);
        if (spL.Count == 0) {
            return null;
        }
        return spL[ Random.Range(0,spL.Count) ];
    }

    // Find the nearest SpawnPoint of a specific PickUp type
    // Pass in an ignoreSP to ignore one SpawnPoint (covers the case when we've gotten there, and there is no item)
    public SpawnPoint FindClosestSP(PickUp.eType puType, SpawnPoint ignoreSP=null) {
        List<SpawnPoint> spL = GetSPList(puType);

        return ClosestSPFromList(spL, ignoreSP);
    }
    #endregion

    #region JB_Strat_Pattern
    public enum eStrat { none, explore, wander }

    [System.Serializable]
    public class UtilRec {
        public eStrat   stratType;
        public bool     enabled;
        public Vector2  utilMinMax;
//        public AnimationCurve curve;
        public System.Func<float,float> curveFunc = x => x; // This is a delegate used for Lambda expressions
        public float    u;
        public Vector3  dest;
        [System.NonSerialized] // strat is NonSerialized, so it's not controlled by the Unity Inspector
        public JBStrat  strat;
    }


    abstract public class JBStrat {
        protected UtilRec uRec;
        public JBStrat(UtilRec ur) {
            uRec = ur;
        }

        public virtual float Utility(HT a) {
            if (!uRec.enabled) {
                return 0;
            }
            // If it is enabled, then curve the value
            uRec.u = uRec.curveFunc(uRec.u);
            // Also apply the min and max values to the curved u
            uRec.u = uRec.utilMinMax.x + (uRec.u * (uRec.utilMinMax.y-uRec.utilMinMax.x));
            return uRec.u;
        }
    }

    /// <summary>
    /// Seek the nearest unknown SpawnPoint
    /// </summary>
    public class JBS_Explore : JBStrat {
        public JBS_Explore(UtilRec ur) : base (ur) { // ": base(ur)" calls the JBStrat constructor and passes in ur

        }

        public override float Utility(HT a) {
            // By declaring these classes INSIDE of JBAgent_Utility, they gain access to JBAgent_Utility.MEM
            if (MEM[PickUp.eType.none].Count == 0) {
                // Everything has been discovered!!!
                // If all SpawnPoints are known, set dest to the origin and return minimum utility
                uRec.dest = Vector3.zero;
                uRec.u = 0;
                return uRec.u;
            }

            // Looks like we still have some exploring to do!
            // Return the closest unknown SpawnPoint and try to explore it.
            uRec.dest = a.ClosestSPFromList( MEM[PickUp.eType.none] ).pos;
            uRec.u = 1;

            // The last line of ALL of these needs to be return base.Utility(a)
            //   because base.Utility(a) applies both the curve and the utilMinMax limits
            return base.Utility(a);
        }
    }

    public class JBS_Wander : JBStrat {
        public JBS_Wander(UtilRec ur) : base (ur) { // ": base(ur)" calls the JBStrat constructor and passes in ur
            
        }

        SpawnPoint sPoint = null;
        public override float Utility(HT a) {
            if (sPoint != null) {
                if ((a.pos - sPoint.pos).magnitude < a.targetProximity) {
                    // We're close enough, so lose this sPoint
                    sPoint = null;
                }
            }

            if (sPoint == null) {
                // Choose a random SpawnPoint of the item type
                sPoint = a.RandomSP();
            }

            // Return the maximum wander utility
            uRec.dest = sPoint.pos;
            uRec.u = 1;

            // The last line of ALL of these needs to be return base.Utility(a)
            //   because base.Utility(a) applies both the curve and the utilMinMax limits
            return base.Utility(a);
        }
    }

    

    #endregion



    #region EnemyTracker


    #endregion

}

