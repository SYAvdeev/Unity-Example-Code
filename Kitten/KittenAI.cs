using System;
using System.Collections.Generic;
using UnityEngine;
using FluentBehaviourTree;
using UnityEngine.AI;
using System.Collections;
using RootMotion.FinalIK;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class KittenAI : MonoBehaviour
{
    KittenAI() { }
    static KittenAI instance;
    public static KittenAI GetInstance()
    {
        if (!instance)
        {
            instance = FindObjectOfType<KittenAI>();
        }
        return instance;
    }

    AimIK KittenAim;
    Transform Aim;
    float AimRadius;

    public List<AudioClip> MeowClips = new List<AudioClip>();
    AudioSource MeowSource;

    public Transform KittenHead;
    public Transform LookForwardPosition;

    NavMeshAgent NavMeshAgent;
    Animator KittenAnimator;

    public KittenHealth KittenHealth;
    public KittenInteraction KittenInteraction;

    public Cat_controller FatherCat;
    IBehaviourTreeNode CurrentBehaviour;

    IBehaviourTreeNode RootBehaviour;
    IBehaviourTreeNode CheckPissBehaviour;
    IBehaviourTreeNode CheckPoopBehaviour;
    IBehaviourTreeNode EatBehaviour;
    IBehaviourTreeNode DrinkBehaviour;
    IBehaviourTreeNode SleepBehaviour;
    IBehaviourTreeNode PlayBehaviour;
    
    public float FortRadius;

    public GameObject Bed;

    public KittenBowl FoodBowl;
    public KittenBowl WaterBowl;

    float BowlMemoryTime = 60f;

    const int maxTrayPoops = 6;
    int trayPoopsNum = 0;
    public GameObject Tray;
    bool IsTrayClean = true;

    public GameObject PoopPrefab;
    public Transform PoopPosition;
    List<GameObject> Poops = new List<GameObject>();

    enum TreeState { Ready, Locked }
    TreeState CurrentState = TreeState.Ready;
    bool IsBegging = false;

#if UNITY_EDITOR
    public bool Debug = true;
    public string DebugPreviousPlans = "";
    public string DebugCurrentPlans = "";
#endif

    // Use this for initialization
    void Awake()
    {
        if (FatherCat == null)
            FatherCat = FindObjectOfType<Cat_controller>();

        Aim = LookForwardPosition;

        KittenAim = gameObject.GetComponent<AimIK>();
        KittenAnimator = gameObject.GetComponent<Animator>();
        KittenHealth = gameObject.GetComponent<KittenHealth>();
        KittenInteraction = gameObject.GetComponent<KittenInteraction>();
        NavMeshAgent = gameObject.GetComponent<NavMeshAgent>();
        MeowSource = gameObject.GetComponent<AudioSource>();

        CheckPissBehaviour = BuildCheckDefecationStatus(t =>
        {
            return CheckValue(KittenHealth.PeeValue);
        }, Piss, "piss", t =>
        {
            return ReloadNode(CheckPissBehaviour);
        });

        CheckPoopBehaviour = BuildCheckDefecationStatus(t =>
        {
            return CheckValue(KittenHealth.PoopValue);
        }, Poop, "poop", t =>
        {
            return ReloadNode(CheckPoopBehaviour);
        });

        EatBehaviour = BuildCheckSaturationStatus("food", FoodBowl, Eat, t =>
        {
            return CheckValue(KittenHealth.hunger);
        }, t =>
        {
            return BehaviourTreeStatus.Completed;//ReloadNode(EatBehaviour);
        });

        DrinkBehaviour = BuildCheckSaturationStatus("water", WaterBowl, Drink, t =>
        {
            return CheckValue(KittenHealth.waterThirst);
        }, t =>
        {
            return BehaviourTreeStatus.Completed; //return ReloadNode(DrinkBehaviour);
        });

        SleepBehaviour = BuildCheckSleepNode(Sleep, t =>
        {
            return CheckValue(KittenHealth.Tiredness);
        });

        PlayBehaviour = BuildPlayBehavoiur();

        RootBehaviour = BuildRootBehaviour();

        CurrentBehaviour = RootBehaviour;
    }

    BehaviourTreeStatus ReloadNode(IBehaviourTreeNode node)
    {
        node.Reload();
        return BehaviourTreeStatus.Success;
    }

    BehaviourTreeStatus CheckValue(float value)
    {
        return (value < 70f) ? BehaviourTreeStatus.Success : BehaviourTreeStatus.Failure;
    }

    IBehaviourTreeNode BuildRootBehaviour()
    {
        var treeBuilder = new BehaviourTreeBuilder();

        return treeBuilder

            .Parallel("Check needs")
                .Sequence("Check defecation")
                    .Splice(CheckPissBehaviour)
                    .Splice(CheckPoopBehaviour)
                .End()
                .Sequence("Check basic needs")
                    .Splice(EatBehaviour)
                    .Splice(DrinkBehaviour)
                    .Splice(SleepBehaviour)
                .End()
                .Sequence("Play")
                    .Splice(PlayBehaviour)
                .End()
            .End()

            .Build();
    }

    BehaviourTreeStatus IsStateNormal()
    {
        return (CurrentState == TreeState.Ready) ? BehaviourTreeStatus.Success : BehaviourTreeStatus.Failure;
    }

    public void LockOn(string animatorName, bool lockState = true)
    {
        if(lockState)
            CurrentState = TreeState.Locked;
        
        NavMeshAgent.enabled = false;
        KittenAnimator.CrossFade(animatorName, 0.2f);
    }

    void LockOnBegging(string animatorName, bool lockState = true)
    {
        LockOn(animatorName, lockState);
        IsBegging = true;
    }

    public void LockOff()
    {
        lockOffTime = Time.timeSinceLevelLoad;
        KittenAnimator.CrossFade("NormalState", 0.2f);
        CurrentState = TreeState.Ready;
        NavMeshAgent.enabled = true;
    }

    void LockOffBegging()
    {
        IsBegging = false;
        LockOff();
    }

    public void LockOffInteracting()
    {
        StartCoroutine(LockOffInteractingRoutine());
    }

    IEnumerator LockOffInteractingRoutine()
    {
        KittenAnimator.CrossFade("SitOff", 0.2f);
        yield return new WaitForSeconds(0.9f);
        LockOff();
    }

    void WriteDebugString(string plans)
    {
#if UNITY_EDITOR
        if (Debug)
        {
            DebugPreviousPlans = DebugCurrentPlans;
            DebugCurrentPlans = plans;
        }
#endif
    }

    #region Defecation
    IBehaviourTreeNode BuildCheckDefecationStatus(Func<TimeData, BehaviourTreeStatus> checkStatusFn, Action<bool> defecatingFn, string nameOfDefecation, Func<TimeData, BehaviourTreeStatus> reloadNodeFn)
    {
        var treeBuilder = new BehaviourTreeBuilder();
        return treeBuilder
            .Sequence("If state == normal")
                .Do("Is State == Normal", t => { return IsStateNormal(); })
                .Parallel("Parallel" + nameOfDefecation)

                    .Selector("Check " + nameOfDefecation + " status")
                        .Do("Is " + nameOfDefecation + " normal", checkStatusFn, true)
                        .Selector("Check where to " + nameOfDefecation, true)
                            .Sequence("Try to " + nameOfDefecation + " tray", true)
                                .Do("Is tray clean", t =>
                                {
                                    WriteDebugString("Is tray clean");

                                    return (IsTrayClean) ? BehaviourTreeStatus.Success : BehaviourTreeStatus.Failure;
                                }, true)
                                .Do("Move to tray to " + nameOfDefecation, t =>
                                {
                                    WriteDebugString("Move to tray to " + nameOfDefecation);

                                    Aim = Tray.transform;
                                    return MoveTo(Tray.transform.position, nameOfDefecation);
                                }, true)
                                .Do(nameOfDefecation + " to tray", t =>
                                {
                                    WriteDebugString(nameOfDefecation + " to tray");
                                    //NavMeshAgent.isStopped = true;

                                    //IsTrayClean = false;

                                    defecatingFn.Invoke(true);

                                    return BehaviourTreeStatus.Success;
                                }, true)
                            .End()
                            .Do(nameOfDefecation + " to floor", t =>
                            {
                                WriteDebugString(nameOfDefecation + " to floor");

                                //создаётся новая какаха и добавляется в список
                                defecatingFn.Invoke(false);

                                return BehaviourTreeStatus.Success;
                            }, true)
                        .End()
                    .End()
                    .Do("Reload " + nameOfDefecation, reloadNodeFn)

                .End()
            .End()
            .Build();
    }




    #endregion

    #region Saturation

    float CurBegTime = 0f;
    IBehaviourTreeNode BuildCheckSaturationStatus(string nameOfSaturation, KittenBowl bowl, Action saturatingFn, Func<TimeData, BehaviourTreeStatus> checkStatusFn, Func<TimeData, BehaviourTreeStatus> reloadNodeFn)
    {
        var treeBuilder = new BehaviourTreeBuilder();

        return treeBuilder
            .Sequence("If state == normal", true)
                .Do("Is State == Normal", t => { return IsStateNormal(); })
                .Parallel("Parallel" + nameOfSaturation)

                    .Selector("Check " + nameOfSaturation)
                        .Do("Is " + nameOfSaturation + " ok", checkStatusFn)
                        .Selector("Try to saturate" + nameOfSaturation)
                            .Sequence("Try to saturate " + nameOfSaturation + " from bowl", true)
                                .Do("Does remember that bowl is empty", t =>
                                {
                                    WriteDebugString("Does remember that bowl is empty");

                                    return (bowl.IsEmptyMemory()) ? BehaviourTreeStatus.Failure : BehaviourTreeStatus.Success;

                                }, true)
                                .Do("Move to bowl", t =>
                                {
                                    WriteDebugString("Move to bowl");

                                    Aim = bowl.gameObject.transform;
                                    return MoveTo(bowl.StopPoint.position, nameOfSaturation);
                                }, true)
                                .Do("Check " + nameOfSaturation + " in bowl", t =>
                                {
                                    WriteDebugString("Check " + nameOfSaturation + " in bowl");

                                    if (bowl.Check())
                                        return BehaviourTreeStatus.Failure;
                                    else
                                        return BehaviourTreeStatus.Success;
                                }, true)
                                .Do("Eat " + nameOfSaturation + " bowl", t =>
                                {
                                    WriteDebugString("Eat " + nameOfSaturation + " bowl");

                                    saturatingFn.Invoke();
                                    return BehaviourTreeStatus.Running;
                                }, true)
                            .End()
                            .Sequence("Begging sequence")
                                .Do("Is father in fort", t =>
                                {
                                    WriteDebugString("Is father in fort");
                                    
                                    if ((FatherCat.transform.position - OurFort.GetInstance().transform.position).sqrMagnitude < FortRadius * FortRadius)
                                    {
                                        Aim = FatherCat.HeadJoint.transform;
                                        return BehaviourTreeStatus.Success;
                                    }
                                    else
                                        return BehaviourTreeStatus.Failure;
                                })
                                .Sequence("Beg father")
                                    .Do("Meow", t => { Meow(); return BehaviourTreeStatus.Success; })
                                    .Selector("Try to beg")
                                        .Sequence("Father near")
                                            .Do("Is father near", t =>
                                            {
                                                WriteDebugString("Is father near");
                                                if ((FatherCat.transform.position - transform.position).sqrMagnitude < 1f * 1f)
                                                    return BehaviourTreeStatus.Success;
                                                else
                                                    return BehaviourTreeStatus.Failure;
                                            })
                                            .Do("Beg cat", t =>
                                            {
                                                WriteDebugString("Beg cat");
                                                if (CurBegTime < 20f)
                                                {
                                                    if ((CurBegTime > 6f && CurBegTime < 9f) ||
                                                        (CurBegTime > 15f && CurBegTime < 18f))
                                                    {
                                                        BegCat();
                                                    }

                                                    CurBegTime += Time.deltaTime;
                                                    return BehaviourTreeStatus.Running;
                                                }
                                                else
                                                {
                                                    CurBegTime = 0f;
                                                    return BehaviourTreeStatus.Success;
                                                }
                                            })
                                        .End()
                                        .Sequence("Move and beg")
                                            .Do("Move to cat", t =>
                                            {
                                                WriteDebugString("Move to cat");
                                                CurBegTime += Time.deltaTime;
                                                return MoveTo(FatherCat.transform.position, nameOfSaturation, 1f, 1f, 100f);
                                            })
                                            .Do("Beg cat", t =>
                                            {
                                                WriteDebugString("Beg cat");
                                                if (CurBegTime < 20f)
                                                {
                                                    if ((CurBegTime > 6f && CurBegTime < 9f) ||
                                                       (CurBegTime > 15f && CurBegTime < 18f))
                                                    {
                                                        BegCat();
                                                    }

                                                    CurBegTime += Time.deltaTime;
                                                    return BehaviourTreeStatus.Running;
                                                }
                                                else
                                                {
                                                    CurBegTime = 0f;
                                                    return BehaviourTreeStatus.Success;
                                                }
                                            })
                                        .End()
                                    .End()
                                .End()
                            .End()
                        .End()
                    .End()
                    .Do("Reload " + nameOfSaturation, reloadNodeFn)

                .End()
            .End()

            .Build();
    }

    #endregion

    #region Sleep

    IBehaviourTreeNode BuildCheckSleepNode(Action sleepFn, Func<TimeData, BehaviourTreeStatus> checkStatusFn)
    {
        var treeBuilder = new BehaviourTreeBuilder();

        return treeBuilder
            .Sequence("If state == normal")
                .Do("Is State == Normal", t => { return IsStateNormal(); })
                .Parallel("Parallel sleep")
                    .Selector("Check sleepness")
                        .Do("Is sleepness ok", checkStatusFn)
                        .Selector("Try to sleep", true)
                            .Sequence("Try to sleep in bed")
                                .Do("Move to bed", t =>
                                {
                                    WriteDebugString("Move to bed");
                                    Aim = Bed.transform;
                                    return MoveTo(Bed.transform.position, "sleep");
                                })
                                .Do("Sleep", t =>
                                {
                                    WriteDebugString("Sleep");
                                    sleepFn.Invoke();
                                    return BehaviourTreeStatus.Success;
                                })
                            .End()
                            .Do("Sleep on floor", t =>
                            {
                                WriteDebugString("Sleep on floor");
                                sleepFn.Invoke();
                                return BehaviourTreeStatus.Success;
                            })
                        .End()
                    .End()
                    .Do("Reload behavior", t =>
                    {
                        CurrentBehaviour.Reload();
                        return BehaviourTreeStatus.Success;
                    })
                .End()
            .End()

            .Build();
    }

    #endregion

    #region Routines

    void Drink()
    {
        StartCoroutine(Coroutine_Rotate(WaterBowl.gameObject.transform.position - transform.position));
        StartCoroutine(DrinkRoutine("Eat"));
    }

    IEnumerator DrinkRoutine(string animationName, string endAnimation = "", float endAnimationTime = 0f)
    {
        LockOn(animationName);

        while (KittenHealth.waterThirst > 5f)
        {
            if(WaterBowl.Value <= 0f) break;

            KittenHealth.waterThirst -= 10f * Time.deltaTime;
            KittenHealth.hunger = Mathf.Max(KittenHealth.hunger, 0f);

            yield return null;
        }

        yield return StartCoroutine(EndSaturation(animationName, endAnimation, endAnimationTime));
    }

    void Eat()
    {
        StartCoroutine(Coroutine_Rotate(FoodBowl.gameObject.transform.position - transform.position));
        StartCoroutine(EatRoutine( "Eat"));
    }

    IEnumerator EatRoutine(string animationName, string endAnimation = "", float endAnimationTime = 0f)
    {
        LockOn(animationName);

        while (KittenHealth.waterThirst > 5f)
        {
            if (FoodBowl.Value <= 0f) break;

            KittenHealth.waterThirst -= 3f * Time.deltaTime;
            KittenHealth.hunger = Mathf.Max(KittenHealth.hunger, 0f);

            yield return null;
        }

        yield return StartCoroutine(EndSaturation(animationName, endAnimation, endAnimationTime));
    }

    

    void Piss(bool isInTray)
    {
        StartCoroutine(PissRoutine("Poop"));
    }

    IEnumerator PissRoutine(string animationName, string endAnimation = "", float endAnimationTime = 0f)
    {
        LockOn(animationName);

        while (KittenHealth.PeeValue > 5f)
        {
            KittenHealth.PeeValue -= 30f * Time.deltaTime;
            KittenHealth.PeeValue = Mathf.Max(KittenHealth.PeeValue, 0f);

            yield return null;
        }

        yield return StartCoroutine(EndSaturation(animationName, endAnimation, endAnimationTime));
    }

    void Sleep()
    {
        StartCoroutine(SleepRoutine("SleepStart"));
    }

    IEnumerator SleepRoutine(string animationName, string endAnimation = "", float endAnimationTime = 0f)
    {
        LockOn(animationName);

        while (KittenHealth.Tiredness > 5f)
        {
            KittenHealth.Tiredness -= Time.deltaTime;
            KittenHealth.Tiredness = Mathf.Max(KittenHealth.Tiredness, 0f);

            yield return null;
        }

        yield return StartCoroutine(EndSaturation(animationName, endAnimation, endAnimationTime));
    }

    void Poop(bool isInTray)
    {
        StartCoroutine(PoopRoutine(isInTray));
    }

    IEnumerator PoopRoutine(bool isInTray)
    {
        LockOn("SitOn");
        yield return new WaitForSeconds( 1.7f );
        KittenAnimator.CrossFade("Poopting", 0.2f);
        KittenHealth.PoopValue = 0f;
    }

    IEnumerator EndSaturation(string animationName, string endAnimation = "", float endAnimationTime = 0f)
    {
        if (endAnimationTime > 0f)
        {
            KittenAnimator.CrossFade(endAnimation, 0.2f);
            yield return new WaitForSeconds(endAnimationTime);
        }

        KittenAnimator.CrossFade("NormalState", 0.2f);
        CurrentState = TreeState.Ready;
        NavMeshAgent.enabled = true;

        yield return null;
    }

    void BegCat()
    {
        StartCoroutine(Coroutine_Rotate(FatherCat.transform.position - transform.position));
        StartCoroutine(BeggingRoutine());
    }

    IEnumerator BeggingRoutine()
    {
        LockOnBegging("SitOn");
        yield return new WaitForSeconds(1.7f);
        KittenAnimator.CrossFade("Pray", 0.2f);
        CurBegTime += 4.7f;
    }
    
    bool isMeow = false;
    void Meow()
    {
        if(!isMeow)
        {
            StopCoroutine(MeowRoutine());
            StartCoroutine(MeowRoutine());
        }
    }

    IEnumerator MeowRoutine()
    {
        isMeow = true;
        MeowSource.clip = MeowClips[(int)(UnityEngine.Random.value * MeowClips.Count)];
        MeowSource.Play();
        KittenAnimator.CrossFade("Miu", 0.1f, 1);

        yield return new WaitForSeconds(MeowSource.clip.length + UnityEngine.Random.value * 0.5f + 0.3f);

        isMeow = false;
    }


    //IEnumerator SaturateRoutine(Func<bool> checkValue, Action saturateFn, string animationName, string endAnimation = "", float endAnimationTime = 0f)
    //{
    //    LockOn(animationName);

    //    while (checkValue.Invoke())
    //    {
    //        saturateFn.Invoke();
    //        yield return null;
    //    }

    //    if (endAnimationTime > 0f)
    //    {
    //        KittenAnimator.CrossFade(endAnimation, 0.2f);
    //        yield return new WaitForSeconds(endAnimationTime);
    //    }

    //    KittenAnimator.CrossFade("NormalState", 0.2f);
    //    CurrentState = TreeState.Ready;
    //    NavMeshAgent.enabled = true;

    //    yield return null;
    //}



    BehaviourTreeStatus MoveTo(Vector3 destination, string reason, float stopDistance = 0f, float acceleration = 1f, float stopAcceleration = 1f)
    {
        if (NavMeshAgent.stoppingDistance != stopDistance)
            NavMeshAgent.stoppingDistance = stopDistance;

        if (NavMeshAgent.acceleration != acceleration)
            NavMeshAgent.acceleration = acceleration;

        NavMeshAgent.SetDestination(destination);

        if (NavMeshAgent.pathStatus == NavMeshPathStatus.PathInvalid)
        {
            return BehaviourTreeStatus.Failure;
        }
        else if (NavMeshAgent.pathPending)
        {
            return BehaviourTreeStatus.Running;
        }
        else if (NavMeshAgent.remainingDistance <= NavMeshAgent.stoppingDistance)
        {
            NavMeshAgent.acceleration = stopAcceleration;
            return BehaviourTreeStatus.Success;
        }
        else
        {
            //if(!NavMeshAgent.isStopped)
                //KittenAnimator.SetFloat("Speed", NavMeshAgent.velocity.magnitude);
            return BehaviourTreeStatus.Running;
        }
    }

    
    public IEnumerator Coroutine_Rotate(Vector3 direction)
    {
        float coroutineRotateTimer = 0f;
        while (coroutineRotateTimer < .5f)
        {
            coroutineRotateTimer += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 4f * Time.deltaTime);
            UnityEngine.Debug.DrawRay(transform.position, direction.normalized , Color.magenta);
            yield return null;
        }

        coroutineRotateTimer = 0f;
        yield break;
    }

    #endregion

    //float StateTimeCounter = 0f;
    void Update()
    {
        HeadSolver();

        KittenAnimator.SetFloat("Speed", NavMeshAgent.velocity.magnitude);
        KittenAnimator.SetFloat("Health", Mathf.Lerp(0f, 1f,  ( KittenHealth.fCurrentHealth * 2f ) / KittenHealth.fMaxHealth));
        

        if (CurrentState == TreeState.Ready)
        {
            CurrentBehaviour.Tick(new TimeData(Time.deltaTime));
        }
        else if (CurrentState == TreeState.Locked)
        {
            if (IsBegging)
                Meow();
        }
    }

    void HeadSolver()
    {
        if (Aim != null)
        {
            Vector3 pos = Aim.position;
            float sqrDist = (Aim.position - transform.position).sqrMagnitude;

            if (sqrDist < 16f && sqrDist > .025f)
            {
                pos = (Aim.position - KittenHead.position).normalized * 2f + KittenHead.position;
            }
            else
            {
                pos = LookForwardPosition.position;
            }

            KittenAim.solver.IKPosition = Vector3.Lerp(KittenAim.solver.IKPosition, pos, 3f * Time.deltaTime);
        }
    }
        

    #region Playing

    enum EPlayingState { Normal, HunterMode, Sit }
    EPlayingState PlayingState = EPlayingState.Normal;

    IBehaviourTreeNode BuildPlayBehavoiur()
    {
        var treeBuilder = new BehaviourTreeBuilder();

        return treeBuilder

            .Parallel("Play")
                
                .Sequence("Change toy")
                    .Condition("Are any toys", t => { return (Toys.Length > 0) && (Time.timeSinceLevelLoad - TimeOfToyChange > 3) && (Playfull < 75); })
                    .Condition("Coroutine finished", t => { return findtoy == null; })
                    .Do("", t => { findtoy = StartCoroutine(FindMostInterestingToy()); return BehaviourTreeStatus.Success; })
                .End()
                .Sequence("If Toy != null")
                    .Condition("Is Toy != null", t => { return Toy != null; })
                    .Selector("States")
                        .Sequence("Normal")
                            .Condition("Is Normal state", t => { return PlayingState == EPlayingState.Normal; })
                            .Selector("Play or move")
                                .Sequence("Playing movements")
                                    .Condition("Enough time passed", t => { return Time.timeSinceLevelLoad - lockOffTime > 3; })
                                    .Selector("Choose playing movement")
                                        .Sequence("Attack floor")
                                            .Condition("Is position right", t =>
                                            {
                                                return (ToyHeight < 0.2f && DistanceBetwenInterest < 0.3 && angleToy < 30 && angleToy > -30);
                                            })
                                            .Do("Attack!!!", t => { LockOn("attack_floor"); return BehaviourTreeStatus.Success; })
                                        .End()
                                        .Sequence("Attack bigjump")
                                            .Condition("Is position right", t =>
                                            {
                                                return (ToyHeight > 0.5f && ToyHeight < 1f && DistanceBetwenInterest < 0.3f && angleToy < 70 && angleToy > -70);
                                            })
                                            .Do("Attack!!!", t => { LockOn("attack_bigjump"); return BehaviourTreeStatus.Success; })
                                        .End()
                                        .Sequence("Huntermode")
                                            .Condition("Is position right", t =>
                                            {
                                                return (ToyHeight < 0.5f && DistanceBetwenInterest > 0.8f && DistanceBetwenInterest < 1.2f && PointSpeed > 0.2f && currentSpeed > 0.2f);
                                            })
                                            .Do("Huntermode on", t => { LockOn("Huntermode", false); PlayingState = EPlayingState.HunterMode; return BehaviourTreeStatus.Success; })
                                        .End()
                                        .Sequence("Sit")
                                            .Condition("Is position right", t =>
                                            {
                                                return (ToyHeight > 0.7f && DistanceBetwenInterest > 0.2f && angleToy < 70 && angleToy > -70 && currentSpeed < 0.3f);
                                            })
                                            .Do("Sit on", t => { LockOn("SitOn", false); PlayingState = EPlayingState.Sit; return BehaviourTreeStatus.Success; })
                                        .End()
                                        .Sequence("Standup")
                                            .Condition("Is position right", t =>
                                            {
                                                return (ToyHeight > 0.2f && ToyHeight < 0.6f && DistanceBetwenInterest < 0.5f && (angleToy < 40 && angleToy > -40) && currentSpeed < 0.3f);
                                            })
                                            .Do("Stand up", t => { LockOn("Standup"); return BehaviourTreeStatus.Success; })
                                        .End()
                                    .End()
                                .End()
                                .Do("Move to toy", t =>
                                {
                                    Aim = Toy;
                                    return MoveTo(Aim.position, " toy");
                                })
                            .End()
                        .End()
                        .Sequence("HunterMode")
                            .Condition("Is HunterMode state", t => { return PlayingState == EPlayingState.HunterMode; })
                            .Sequence("Exit hunter mode")
                                .Condition("Is position right", t =>
                                {
                                    return (ToyHeight < 0.5f && DistanceBetwenInterest > 1.5f && WantRun);
                                })
                                .Do("Stand up", t => {
                                    PlayingState = EPlayingState.Normal;
                                    LockOff();
                                    return BehaviourTreeStatus.Success;
                                })
                            .End()
                        .End()
                        .Sequence("Sit")
                            .Condition("Is Sit state", t => { return PlayingState == EPlayingState.Sit; })
                            .Sequence("Exit sit state")
                                .Condition("Is position right", t =>
                                {
                                    return (ToyHeight < 0.5f || DistanceBetwenInterest > 1.5f || (angleToy > 100 || angleToy < -100));
                                })
                                .Do("Stand up", t => {
                                    PlayingState = EPlayingState.Normal;
                                    LockOn("SitOff");
                                    return BehaviourTreeStatus.Success;
                                })
                            .End()
                        .End()
                    .End()
                .End()

            .End()

            .Build();
    }

    bool WantRun
    {
        get
        {
            if (LostSightOfTarget) return true;
            return DistanceBetwenInterest > DistanceForChase() * 2f;
        }
    }

    bool LostSightOfTarget
    {
        get
        {
            NavMeshHit hit;
            return NavMeshAgent.Raycast(CharacterHead.position, out hit);
        }
    }


    [Range(0, 100)]
    public float
        Playfull = 0; //Игривость

    private float
        minPlayfull = 50, //Минимальное значение, до которого Playfull может опускаться
        minTired = 0; //Минимальное значение, до которого Tired может опускаться

    public Transform CharacterHead;
    public KittensToy[] Toys;

    private Transform Toy;
    private float timefortoychange = 0;
    private Vector3 lookAtPosition = Vector3.zero;
    private float
        lockOffTime = 0,
        currentSpeed = 0,
        PointSpeed = 0;

    float angleToy
    {
        get
        {
            float an = Vector3.Angle(transform.forward, Toy.position - transform.position);
            Vector3 cross = Vector3.Cross(transform.forward, Toy.position - transform.position);
            if (cross.y < 0) an = -an;
            return an;
        }
    }

    float angle(Vector3 pos)
    {
        float an = Vector3.Angle(transform.forward, pos - transform.position);
        Vector3 cross = Vector3.Cross(transform.forward, pos - transform.position);
        if (cross.y < 0) an = -an;
        return an;
    }

    float DistanceForChase()
    {
        return (100 - Playfull) * 0.035f;
    }

    bool NeedForTurnAround
    {
        get
        {
            bool turnaround = angleToy > 50 || angleToy < -50;
            if (turnaround)
            {
                if (DistanceBetwenInterest > 0.3f) return true;
            }
            return false;
        }
    }

    float DistanceBetwenInterest
    {
        get
        {
            Vector2
                thisObject = new Vector2(transform.position.x, transform.position.z),
                target = new Vector2(Toy.position.x, Toy.position.z);
            return Vector3.Distance(thisObject, target);
        }
    }

    float ToyHeight
    {
        get
        {
            return Toy.position.y - transform.position.y;
        }
    }

    bool WantChase
    {
        get
        {

            //if (Toy != null)
            //{
            //    if (Toy.position.y - transform.position.y < 0.3f && DistanceBetwenInterest > 1)
            //    {
            //        animator.SetBool("TargetOnTheGround", true);
            //    }
            //    else
            //    {
            //        animator.SetBool("TargetOnTheGround", false);
            //    }

            //    if (LostSightOfTarget && CanWalk) return true;
            //    if (NeedForTurnAround) return true;
            //    //if (Catchable < 2f) return false;
            //    if (DistanceBetwenInterest < 2f && DistanceBetwenInterest > 0.5f && PointSpeed > 0.5f) return true;

            //    if (
            //        (DistanceBetwenInterest / DistanceForChase() < 0.3f && PointSpeed > 1)
            //        || (PointSpeed == 0 && DistanceBetwenInterest / DistanceForChase() > 0.4f)
            //        || PointSpeed < Playfull / 30f) return false;

            //    //return true;
            //}

            return false;
        }
    }

    void correctPlayfullMood()
    {
        if (!WantChase)
        {
            Playfull = Mathf.Lerp(Playfull, minPlayfull, Time.fixedDeltaTime * 0.2f);
            return;
        }
        else
        {
            Playfull = Mathf.Lerp(Playfull, 100, Time.fixedDeltaTime * 0.6f);
        }
    }

    Vector3 LastToyPosition = Vector3.zero;
    float toyMovementSpeed()
    {
        Vector3 point = Toy.position;
        if (LastToyPosition == Vector3.zero)
        {
            LastToyPosition = point;
            return 0;
        }

        float ToySpeed = Vector3.Distance(point, LastToyPosition) / Time.fixedDeltaTime;
        LastToyPosition = point;
        return ToySpeed;
    }
    float TimeOfToyChange = -100;
    void ChangeToy()
    {
        if (Toys == null || Toys.Length < 2) return;
        if (Time.timeSinceLevelLoad - TimeOfToyChange < 3) return;
        if (Playfull < 75) FindToy();
    }
    Coroutine findtoy;
    [ContextMenu("Change Toy")]
    void FindToy()
    {
        TimeOfToyChange = Time.timeSinceLevelLoad;
        if (findtoy != null) StopCoroutine(findtoy);
        findtoy = StartCoroutine(FindMostInterestingToy());

    }
    IEnumerator FindMostInterestingToy()
    {
        foreach (var item in Toys)
        {
            if (item != null) item.Init();
        }

        for (int i = 0; i < 10; i++)
        {
            foreach (var item in Toys)
            {
                if (item != null) item.CalcAttention(transform);
            }

            yield return new WaitForEndOfFrame();
        }

        float bestAttention = 0;

        for (int i = 0; i < Toys.Length; i++)
        {
            if (Toys[i] != null && Toys[i].Attention > bestAttention) bestAttention = Toys[i].Attention;
        }

        foreach (var item in Toys)
        {
            if (item != null && item.Attention == bestAttention)
            {
                if (Toy != item.Object)
                {
                    Toy = item.Object;
                    Playfull = minPlayfull;
                }
            }
        }
    }
    #endregion


    //[Serializable]
    //public class Bowl
    //{
    //    public GameObject GameObject;
    //    public Transform StopPoint;
    //    public bool IsBowlEmpty = false;
    //    public bool IsBowlEmptyMemory = false;
    //    public float Time = 60f;
    //    public float Value = 100f;
    //}

    [Serializable]
    public class Miu
    {
        public float Duration;
        public AudioClip Audio;
    }

    [System.Serializable]
    public class KittensToy
    {
        public Transform Object;
        [HideInInspector] public float Attention = 0;

        [NonSerialized]
        private Vector3 LastPosition = Vector3.zero;

        public void Init()
        {
            Attention = 0;
            LastPosition = Vector3.zero;
        }

        float Size
        {
            get
            {
                float s = (Object.localScale.x + Object.localScale.y + Object.localScale.z) / 3;
                if (s != 3) return s;

                if (Object.parent != null)
                {
                    return Vector3.Distance(Object.position, Object.parent.position);
                }

                return 0.02f;
            }
        }

        float Speed
        {
            get
            {
                if (LastPosition == Vector3.zero)
                {
                    LastPosition = Object.position;
                    return 0;
                }
                float S = Vector3.Distance(LastPosition, Object.position) / Time.fixedDeltaTime;
                LastPosition = Object.position;
                return S;
            }
        }

        public void CalcAttention(Transform kitten)
        {
            Attention += (Size * Speed) / Vector3.Distance(kitten.position, Object.position);
        }
    }
}

#if UNITY_EDITOR

[CustomEditor(typeof(KittenAI))]
public class KittenAIEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
    }

    public void OnSceneGUI()
    {
        var kittenAI = target as KittenAI;

        string debugString = "";
        debugString += "Previous plans: " + kittenAI.DebugPreviousPlans + "\n";
        debugString += "Current plans: " + kittenAI.DebugCurrentPlans + "\n";

        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.green;
        Handles.color = Color.blue;
        Handles.Label(kittenAI.transform.position, debugString, style);
    }
}
#endif