#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameVSM
{
    // Opt-in isolated replay. Never runs during a user's ordinary shift.
    public sealed class MvpReview : MonoBehaviour
    {
        public string Output;
        ShiftClient client;
        ShiftExperience experience;
        FirstPersonController player;
        readonly JArray steps = new();
        readonly JArray journeys = new();
        readonly JArray handChecks = new();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--shift-review-output");
            if(i>=0 && i+1<args.Length)new GameObject("MVP review").AddComponent<MvpReview>().Output=args[i+1];
        }
        IEnumerator Start()
        {
            Application.runInBackground=true;Directory.CreateDirectory(Output);yield return null;
            client=FindAnyObjectByType<ShiftClient>();experience=client.GetComponent<ShiftExperience>();player=FindAnyObjectByType<FirstPersonController>();
            client.SessionFileName="mvp-review-isolated.json";client.Connect("http://127.0.0.1:8011",false);
            yield return Wait();
            yield return Photo("entrance",new Vector3(128,1.72f,-5.7f),new Vector3(112,2,-6));
            yield return Photo("depot",new Vector3(60,2,-6),new Vector3(35,3,-1));
            for(int i=0;i<40 && client.Attempt?["task"] is JObject;i++)
            {
                var world=client.GetComponent<ShiftEnvironment>();
                float motionEnd=Time.realtimeSinceStartup+30;
                float nextPhoto=0;int photo=0;
                Vector3 originalLook=player.View.transform.forward;
                while(world.Transitioning && Time.realtimeSinceStartup<motionEnd)
                {
                    // Keep the conductor in place; only turn their head towards a saloon window.
                    player.LookAt(player.View.transform.position+Vector3.forward*5);
                    if(Time.realtimeSinceStartup>=nextPhoto)
                    {
                        string name="journey-"+experience.Node+"-"+(photo++).ToString("D3")+".png";
                        ScreenCapture.CaptureScreenshot(Path.Combine(Output,name));
                        journeys.Add(new JObject{["frame"]=name,["location"]=world.Location,["speed"]=world.TravelSpeed,
                            ["scenery_x"]=world.Current.transform.position.x,["player_x"]=player.transform.position.x,
                            ["status"]=world.TransitionText});
                        nextPhoto=Time.realtimeSinceStartup+.5f;
                    }
                    yield return null;
                }
                player.LookAt(player.View.transform.position+originalLook*5);
                if(world.Transitioning){Fail("Journey did not finish: "+world.TransitionText);yield break;}
                string node=experience.Node;
                if(node=="boarding2")yield return Photo("boarding",new Vector3(51,3,-5.4f),new Vector3(47.5f,2.1f,-1.4f));
                if(node=="settle")
                {
                    yield return Photo("passengers",new Vector3(42.8f,2.95f,-.24f),new Vector3(38.5f,2.2f,.4f));
                    yield return Photo("window-a",new Vector3(38,2.65f,.1f),new Vector3(38,2.65f,8));
                    yield return new WaitForSeconds(2);
                    yield return Photo("window-b",new Vector3(38,2.65f,.1f),new Vector3(38,2.65f,8));
                    yield return Photo("bistro-stock",new Vector3(5.5f,2.4f,.3f),new Vector3(5,1.85f,-1.05f));
                }
                if(node=="phone")yield return Photo("phone",experience.Passengers[3].Seat+new Vector3(-.4f,1.5f,.8f),experience.Passengers[3].Seat+Vector3.up*1.15f);
                if(node=="quiet1")
                {
                    yield return new WaitForSeconds(1.5f);
                    yield return Photo("luggage",new Vector3(46,2.9f,-.4f),new Vector3(46.7f,2.8f,1.1f));
                }
                if(node=="alight")yield return Photo("alighting",new Vector3(52,3,-5.5f),new Vector3(47.5f,2.1f,-2));
                float end=Time.realtimeSinceStartup+80;
                // Give passengers the aisle: the replay camera must obey the same
                // player obstruction rules as the interactive game.
                if(experience.WaitingForPeople){player.Teleport(new Vector3(48.4f,1.31f,.9f));yield return null;}
                while(experience.WaitingForPeople && Time.realtimeSinceStartup<end)yield return null;
                if(node=="medical_support")yield return Photo("help",experience.Passengers[5].Seat+new Vector3(-1.5f,1.55f,-.7f),experience.Passengers[5].Seat+Vector3.up*1.1f);
                Vector3 target=experience.TargetPosition;
                Vector3 feet=new Vector3(target.x+.9f,1.31f,-.24f);
                if(node=="brief")feet=target+new Vector3(-1.2f,-1.2f,0);
                if(node.StartsWith("boarding"))feet=target+new Vector3(-.9f,-1.2f,-.65f);
                if(node is "platform" or "alight")feet=new Vector3(47.5f,1.31f,-3.5f);
                player.enabled=true;player.Teleport(feet);player.SetMenu(false);yield return null;
                player.LookAt(experience.TargetPosition);
                if(!experience.Interact()){Fail("Cannot interact: "+node);yield break;}
                bool[] capturedHands=new bool[5];
                float[] handStages={.16f,.32f,.45f,.75f,.88f};
                string[] handNames={"hands-pickup","hands-carry","hands-action","hands-place","hands-release"};
                while(experience.Acting || client.Busy)
                {
                    if(node=="bag_action")
                        for(int stage=0;stage<handStages.Length;stage++)
                            if(!capturedHands[stage] && experience.ActionProgress>=handStages[stage])
                            {
                                ScreenCapture.CaptureScreenshot(Path.Combine(Output,handNames[stage]+".png"));
                                capturedHands[stage]=true;
                                var hands=player.GetComponentInChildren<FirstPersonHands>();
                                handChecks.Add(new JObject{["stage"]=handNames[stage],["progress"]=experience.ActionProgress,
                                    ["active"]=hands!=null && hands.Active,["contact_error"]=hands!=null?hands.ContactError:-1,
                                    ["left_hand"]=hands!=null?hands.LeftHand.position.ToString():"missing",
                                    ["right_hand"]=hands!=null?hands.RightHand.position.ToString():"missing",
                                    ["camera"]=player.View.transform.position.ToString(),
                                    ["camera_forward"]=player.View.transform.forward.ToString(),
                                    ["bag"]=experience.Luggage.position.ToString(),
                                    ["bag_viewport"]=player.View.WorldToViewportPoint(experience.Luggage.position).ToString(),
                                    ["grip"]=experience.Luggage.GetComponent<LuggageVisual>().Grip.position.ToString()});
                            }
                    yield return null;
                }
                if(experience.Node==node)
                {
                    var task=client.Attempt["task"];
                    if(!experience.CanChoose(out var reason)){Fail(node+": "+reason);yield break;}
                    experience.Perform((string)task["choices"][0]["id"],(int)task["wait"]>0);
                    while(experience.Acting)yield return null;yield return Wait();
                }
                if(client.Error.Length>0){Fail(client.Error);yield break;}
                steps.Add(new JObject{["node"]=node,["next"]=experience.Node,["revision"]=client.Attempt["revision"]});
            }
            client.LoadCareer();yield return Wait();
            player.SetMenu(true);yield return null;yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,"debrief.png"));yield return new WaitForEndOfFrame();yield return null;
            File.WriteAllText(Path.Combine(Output,"shift-result.json"),new JObject{["status"]=client.Attempt["state"]["status"],["quality"]=client.Attempt["state"]["quality"],["trust"]=client.Attempt["state"]["trust"],["steps"]=steps,["journeys"]=journeys,["hand_checks"]=handChecks,["profile"]=client.Profile,["analytics"]=client.Analytics}.ToString());
            if(!Application.isEditor)Application.Quit();
        }
        IEnumerator Wait(){float end=Time.realtimeSinceStartup+25;while(client.Busy && Time.realtimeSinceStartup<end)yield return null;}
        IEnumerator Photo(string name,Vector3 position,Vector3 target)
        {
            Vector3 original=player.transform.position, local=player.View.transform.localPosition;
            Vector3 lookBack=player.View.transform.position+player.View.transform.forward;
            player.enabled=false;player.SetMenu(false);player.Teleport(position-local);player.LookAt(target);
            for(int i=0;i<12;i++)yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(Output,name+".png"));yield return new WaitForEndOfFrame();yield return null;
            // Camera position must be restored before the next spatial interaction.
            player.Teleport(original);player.View.transform.localPosition=local;player.LookAt(lookBack);player.enabled=true;
        }
        void Fail(string reason){File.WriteAllText(Path.Combine(Output,"shift-result.json"),new JObject{["status"]="failed",["reason"]=reason,["steps"]=steps}.ToString());if(!Application.isEditor)Application.Quit(1);}
    }
}
#endif
