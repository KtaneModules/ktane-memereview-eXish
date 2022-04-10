using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using System;
using UnityEngine.Networking;

public class MemeReviewScript : MonoBehaviour {

    public KMAudio audio;
    public KMBombInfo bomb;

    public KMSelectable[] buttons;
    public MeshRenderer backRenderer;
    public MeshRenderer memeRenderer;
    public Material[] backgroundMats;
    public Material materialMaster;
    public GameObject regenText;
    public GameObject statusLight;

    private Coroutine strikeRoutine;
    private bool loading = false;
    private bool shouldReview = false;
    private bool error = false;

    static int moduleIdCounter = 1;
    int moduleId;
    private bool moduleSolved;

    void Awake()
    {
        moduleId = moduleIdCounter++;
        moduleSolved = false;
        foreach (KMSelectable obj in buttons)
        {
            KMSelectable pressed = obj;
            pressed.OnInteract += delegate () { PressButton(pressed); return false; };
        }
    }

    void Start () {
        statusLight.SetActive(false);
        StartCoroutine(GetMeme(true));
    }

    void PressButton(KMSelectable pressed)
    {
        if (moduleSolved != false && loading != true && Array.IndexOf(buttons, pressed) == 0 && !error)
        {
            regenText.SetActive(true);
            memeRenderer.material = materialMaster;
            StartCoroutine(GetMeme(false));
            return;
        }
        if (moduleSolved != true && loading != true)
        {
            pressed.AddInteractionPunch(0.5f);
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, pressed.transform);
            if (Array.IndexOf(buttons, pressed) == 0 && !shouldReview)
            {
                if (error)
                {
                    if (strikeRoutine != null)
                        StopCoroutine(strikeRoutine);
                    strikeRoutine = StartCoroutine(HandleStrikeRoutine());
                    GetComponent<KMBombModule>().HandleStrike();
                    Debug.LogFormat("[Meme Review #{0}] Regen button pressed. Strike!", moduleId);
                    return;
                }
                regenText.SetActive(true);
                memeRenderer.material = materialMaster;
                Debug.LogFormat("[Meme Review #{0}] Regenerating meme...", moduleId);
                StartCoroutine(GetMeme(true));
            }
            else if (Array.IndexOf(buttons, pressed) == 0 && shouldReview)
            {
                if (strikeRoutine != null)
                    StopCoroutine(strikeRoutine);
                strikeRoutine = StartCoroutine(HandleStrikeRoutine());
                GetComponent<KMBombModule>().HandleStrike();
                Debug.LogFormat("[Meme Review #{0}] Attempted to generate another meme when the current should be sent in for review. Strike!", moduleId);
            }
            else if (Array.IndexOf(buttons, pressed) == 1 && (shouldReview || error))
            {
                moduleSolved = true;
                backRenderer.material = backgroundMats[2];
                if (strikeRoutine != null)
                    StopCoroutine(strikeRoutine);
                GetComponent<KMBombModule>().HandlePass();
                if (error)
                    Debug.LogFormat("[Meme Review #{0}] Review button pressed. Module solved!", moduleId);
                else
                    Debug.LogFormat("[Meme Review #{0}] You sent in the current meme for review, which is correct. Module solved!", moduleId);
            }
            else
            {
                if (strikeRoutine != null)
                    StopCoroutine(strikeRoutine);
                strikeRoutine = StartCoroutine(HandleStrikeRoutine());
                GetComponent<KMBombModule>().HandleStrike();
                Debug.LogFormat("[Meme Review #{0}] You sent in the current meme for review, which is incorrect. Strike!", moduleId);
            }
        }
    }

    IEnumerator GetMeme(bool log)
    {
        loading = true;
        int memeExtension = UnityEngine.Random.Range(0, 2);
        if (memeExtension == 0)
        {
            if (UnityEngine.Random.Range(0, 5) != 0)
            {
                shouldReview = false;
                int timesToGo = UnityEngine.Random.Range(1, 21);
                int ct = 0;
                WWW www = new WWW("https://imgflip.com/m/ai_memes");
                retry:
                while (!www.isDone) { yield return null; if (www.error != null) break; };
                if (www.error == null)
                {
                    List<int> indexes = AllIndexesOf(www.text, "<img class='base-img' src='");
                    if (indexes.Count != 0)
                    {
                        int choice = UnityEngine.Random.Range(0, indexes.Count);
                        string imageLink = GetLinkFromIndex(indexes[choice], www);
                        string nextLink = GetLinkFromIndex(indexes[indexes.Count - 1], www);
                        www = new WWW("https://imgflip.com/m/ai_memes?after=" + nextLink.Substring(16, nextLink.Length - 20));
                        ct++;
                        if (ct < timesToGo)
                            goto retry;
                        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageLink);
                        yield return request.SendWebRequest();
                        if (!request.isNetworkError && !request.isHttpError)
                        {
                            WWW www2 = new WWW("https://imgflip.com/m/ai_memes?sort=hot");
                            while (!www2.isDone) { yield return null; if (www2.error != null) break; };
                            if (www2.error == null)
                            {
                                List<int> indexes2 = AllIndexesOf(www2.text, "<img class='base-img' src='");
                                if (indexes2.Count != 0)
                                {
                                    for (int i = 0; i < indexes2.Count; i++)
                                    {
                                        string imageLink2 = GetLinkFromIndex(indexes2[i], www2);
                                        if (imageLink.Equals(imageLink2))
                                        {
                                            ct--;
                                            goto retry;
                                        }
                                    }
                                    Material mat = new Material(materialMaster);
                                    mat.mainTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                                    memeRenderer.material = mat;
                                    regenText.SetActive(false);
                                    loading = false;
                                    if (log)
                                        Debug.LogFormat("[Meme Review #{0}] Current meme is not on the front page and was received sorting by \"New\".", moduleId);
                                    yield break;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                shouldReview = true;
                WWW www = new WWW("https://imgflip.com/m/ai_memes");
                while (!www.isDone) { yield return null; if (www.error != null) break; };
                if (www.error == null)
                {
                    List<int> indexes = AllIndexesOf(www.text, "<img class='base-img' src='");
                    if (indexes.Count != 0)
                    {
                        int choice = UnityEngine.Random.Range(0, indexes.Count);
                        string imageLink = GetLinkFromIndex(indexes[choice], www);
                        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageLink);
                        yield return request.SendWebRequest();
                        if (!request.isNetworkError && !request.isHttpError)
                        {
                            yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 3f));
                            Material mat = new Material(materialMaster);
                            mat.mainTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                            memeRenderer.material = mat;
                            regenText.SetActive(false);
                            loading = false;
                            if (log)
                                Debug.LogFormat("[Meme Review #{0}] Current meme is on the front page and was received sorting by \"New\".", moduleId);
                            yield break;
                        }
                    }
                }
            }
        }
        else
        {
            if (UnityEngine.Random.Range(0, 5) != 0)
            {
                shouldReview = false;
                int tries = 0;
                redo:
                WWW www = new WWW("https://imgflip.com/m/ai_memes?sort=hot" + "&page=" + UnityEngine.Random.Range(2, 1001));
                while (!www.isDone) { yield return null; if (www.error != null) break; };
                if (www.error == null)
                {
                    List<int> indexes = AllIndexesOf(www.text, "<img class='base-img' src='");
                    if (indexes.Count != 0)
                    {
                        int choice = UnityEngine.Random.Range(0, indexes.Count);
                        string imageLink = GetLinkFromIndex(indexes[choice], www);
                        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageLink);
                        yield return request.SendWebRequest();
                        if (!request.isNetworkError && !request.isHttpError)
                        {
                            WWW www2 = new WWW("https://imgflip.com/m/ai_memes");
                            while (!www2.isDone) { yield return null; if (www2.error != null) break; };
                            if (www2.error == null)
                            {
                                List<int> indexes2 = AllIndexesOf(www2.text, "<img class='base-img' src='");
                                if (indexes2.Count != 0)
                                {
                                    for (int i = 0; i < indexes2.Count; i++)
                                    {
                                        string imageLink2 = GetLinkFromIndex(indexes2[i], www2);
                                        if (imageLink.Equals(imageLink2))
                                            goto redo;
                                    }
                                    yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 3f));
                                    Material mat = new Material(materialMaster);
                                    mat.mainTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                                    memeRenderer.material = mat;
                                    regenText.SetActive(false);
                                    loading = false;
                                    if (log)
                                        Debug.LogFormat("[Meme Review #{0}] Current meme is not on the front page and was received sorting by \"Hot\".", moduleId);
                                    yield break;
                                }
                            }
                        }
                    }
                    else
                    {
                        tries++;
                        if (tries < 1000)
                            goto redo;
                    }
                }
            }
            else
            {
                shouldReview = true;
                WWW www = new WWW("https://imgflip.com/m/ai_memes?sort=hot");
                while (!www.isDone) { yield return null; if (www.error != null) break; };
                if (www.error == null)
                {
                    List<int> indexes = AllIndexesOf(www.text, "<img class='base-img' src='");
                    if (indexes.Count != 0)
                    {
                        int choice = UnityEngine.Random.Range(0, indexes.Count);
                        string imageLink = GetLinkFromIndex(indexes[choice], www);
                        UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageLink);
                        yield return request.SendWebRequest();
                        if (!request.isNetworkError && !request.isHttpError)
                        {
                            yield return new WaitForSeconds(UnityEngine.Random.Range(0f, 3f));
                            Material mat = new Material(materialMaster);
                            mat.mainTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                            memeRenderer.material = mat;
                            regenText.SetActive(false);
                            loading = false;
                            if (log)
                                Debug.LogFormat("[Meme Review #{0}] Current meme is on the front page and was received sorting by \"Hot\".", moduleId);
                            yield break;
                        }
                    }
                }
            }
        }
        error = true;
        regenText.GetComponent<TextMesh>().text = "Error";
        if (log)
            Debug.LogFormat("[Meme Review #{0}] The module failed to fetch memes, press the review button to solve the module.", moduleId);
        loading = false;
    }

    string GetLinkFromIndex(int index, WWW www)
    {
        int indexBegin = index + 27;
        int indexEnd = indexBegin;
        while (www.text[indexEnd] != '\'')
            indexEnd++;
        return www.text.Substring(indexBegin, indexEnd - indexBegin);
    }

    List<int> AllIndexesOf(string str, string value)
    {
        List<int> indexes = new List<int>();
        for (int index = 0; ; index += value.Length)
        {
            index = str.IndexOf(value, index);
            if (index == -1)
                return indexes;
            indexes.Add(index);
        }
    }

    IEnumerator HandleStrikeRoutine()
    {
        backRenderer.material = backgroundMats[1];
        yield return new WaitForSeconds(1f);
        backRenderer.material = backgroundMats[0];
        strikeRoutine = null;
    }

    //twitch plays
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} regen [Presses the regen button] | !{0} review [Presses the review button]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command)
    {
        if (Regex.IsMatch(command, @"^\s*regen\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            buttons[0].OnInteract();
            yield break;
        }
        if (Regex.IsMatch(command, @"^\s*review\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            buttons[1].OnInteract();
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        while (!moduleSolved)
        {
            if (loading)
                yield return true;
            else if (!shouldReview)
                buttons[0].OnInteract();
            else
                buttons[1].OnInteract();
        }
    }
}