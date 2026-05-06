using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System;
using System.Linq;

namespace Game
{
    public class SongPlaying : MonoBehaviour
    {



        enum TokenType
        {
            BPM,        // (120)
            Beats,      // {4}
            Note,       // 4, 3, etc.
            Rest,      // ,
            Slash,      // /
            NewLine,    // \n
            Comment,    // # ...
            SV,         // [1.5]
            HoldNote,   // 1h[4:4]
        }

        class SVEvent
        {
            public float Time { get; }
            public float Multiplier { get; }
            public float Integral { get; }

            public SVEvent(float time, float multiplier, float integral)
            {
                Time = time;
                Multiplier = multiplier;
                Integral = integral;
            }
        }

        class PostInfo
        {
            public int Line { get; }
            public Range Range { get; }

            public PostInfo(int line, Range position)
            {
                Line = line;
                Range = position;
            }

            public override string ToString() => $"{Line}:{Range}";
        }

        class Token : PostInfo
        {
            public Token(TokenType type, string value, int line, Range position)
                : base(line, position)
            {
                Type = type;
                Value = value;
            }

            public Token(TokenType type, string value, int line, int start, int lan = 1)
                : this(type, value, line, new Range(start, start + lan)) { }

            public TokenType Type { get; }
            public string Value { get; }

            public override string ToString() => $"{Type}({Value}) at {base.ToString()}";
        }

        class ErrorPos : Exception
        {
            public PostInfo PositionInfo { get; }


            public ErrorPos(string message, int line, Range position)
                : base($"Error at {line}:{position} - {message}")
            {
                PositionInfo = new PostInfo(line, position);
            }

            public ErrorPos(string message, PostInfo posInfo)
                : base($"Error at {posInfo} - {message}")
            {
                PositionInfo = posInfo;
            }

            public ErrorPos(string message, int line, int start, int lan = 1)
                : this(message, line, new Range(start, start + lan))
            {
            }

            public override string ToString() => $"{Message} (at {PositionInfo})";
        }

        class Note
        {
            public int Lane { get; }
            public float Time { get; }
            public bool IsHold { get; }
            public float EndTime { get; }

            public Note(int lane, float time, bool isHold = false, float endTime = 0f)
            {
                Lane = lane;
                Time = time;
                IsHold = isHold;
                EndTime = endTime;
            }
        }



        public GameObject Note_1, Note_2, Note_3, Note_4;
        public GameObject TargetNote_1, TargetNote_2, TargetNote_3, TargetNote_4;
        public GameObject Judge;
        public Sprite Judge_Perfect, Judge_Great, Judge_Miss;
        public GameObject JudgeAudio;
        public GameObject JudgeTime, Playing;
        public float speed = 2.0f; // Speed at which the note moves
        public float chartOffset = 1.6f; // Offset for the first note

        // Time windows in seconds (convert milliseconds to seconds)
        public float perfectWindow = 33;  // 33ms
        public float greatWindow = 66;    // 66ms
        public float missWindow = 200;

        private List<GameObject> _Note_1_List = new List<GameObject>();
        private List<GameObject> _Note_2_List = new List<GameObject>();
        private List<GameObject> _Note_3_List = new List<GameObject>();
        private List<GameObject> _Note_4_List = new List<GameObject>();

        // Track the time each note is created
        private List<float> _Note_1_Times = new List<float>();
        private List<float> _Note_2_Times = new List<float>();
        private List<float> _Note_3_Times = new List<float>();
        private List<float> _Note_4_Times = new List<float>();

        private List<Vector3> _Note_1_StartPos = new List<Vector3>();
        private List<Vector3> _Note_2_StartPos = new List<Vector3>();
        private List<Vector3> _Note_3_StartPos = new List<Vector3>();
        private List<Vector3> _Note_4_StartPos = new List<Vector3>();

        class HoldNoteObj
        {
            public GameObject Head;
            public GameObject Tail;
            public GameObject Body;
            public float HitTime;
            public float EndTime;
            public bool HeadHit;
            public bool IsDead;
            public Vector3 HeadStartPos;
            public Vector3 TailStartPos;
        }

        private List<HoldNoteObj> _HoldNote_1_List = new List<HoldNoteObj>();
        private List<HoldNoteObj> _HoldNote_2_List = new List<HoldNoteObj>();
        private List<HoldNoteObj> _HoldNote_3_List = new List<HoldNoteObj>();
        private List<HoldNoteObj> _HoldNote_4_List = new List<HoldNoteObj>();

        public GameObject BGM;

        public GameObject timeToSpawnOBJ, songTimeOBJ;

        public GameObject pause;

        Coroutine judgeResetCoroutine;

        public GameObject result;
        public GameObject result_Score, result_Perfect, result_Great, result_Miss, result_Combo, result_Rank, result_APFC;

        private bool playing = false;
        private bool isPause = true;


        private float songTime = -1.5f;
        private bool audioPlayed = false;
        private readonly Dictionary<Note, bool> noteSpawned = new();
        private List<SVEvent> svEvents = new List<SVEvent>();

        float GetIntegralFromZero(float t)
        {
            if (svEvents.Count == 0) return t;
            SVEvent lastEvent = svEvents[0];
            for (int i = 1; i < svEvents.Count; i++)
            {
                if (svEvents[i].Time <= t) lastEvent = svEvents[i];
                else break;
            }
            return lastEvent.Integral + (t - lastEvent.Time) * lastEvent.Multiplier;
        }

        int countPerfect = 0, countGreat = 0, countMiss = 0, combo = 0;
        int totalNotes;
        int score = 0;
        const int maxScore = 1000000;

        int maxcombo = 0;
        int displayedScore = 0; // 用於顯示動畫的分數

        public GameObject countPerfectOBJ, countGreatOBJ, countMissOBJ, countComboOBJ, scoreOBJ;

        List<Note> notes;

        private KeyCode[] keys;

        void Start()
        {
            playing = false;
            KeyManager keyManager = new KeyManager();
            keys = keyManager.GetKeyCodes();
            Playing.GetComponent<TextMeshProUGUI>().text = gameObject.AddComponent<SongSelectScript>().GetSongName();
            AudioClip _BGM = Resources.Load<AudioClip>("Songs/" + gameObject.AddComponent<PlayButton>().GetPlaySong() + "/track");

            BGM.GetComponent<AudioSource>().clip = _BGM;
            var ChartData = Resources.Load<TextAsset>("Songs/" + gameObject.AddComponent<PlayButton>().GetPlaySong() + "/chart");
            if (ChartData == null)
            {
                SceneManager.LoadScene("SongSelect");
                return;
            }
            string chart = ChartData.ToString();
            var tokens = LexicalAnalysis(chart, out var tokenWarnings);
            foreach (var warning in tokenWarnings)
                PrintWarning(chart, warning);

            notes = ParseTokens(tokens, out var noteWarnings);
            foreach (var note in notes)
                Debug.Log($"{note.Time - chartOffset}: {note.Lane}");

            foreach (var warning in noteWarnings)
                PrintWarning(chart, warning);
            foreach (var note in notes)
            {
                noteSpawned[note] = false;
            }

            totalNotes = 0;
            foreach (var note in notes)
            {
                totalNotes += note.IsHold ? 2 : 1;
            } // 計算總音符數量
            StartCoroutine(StartSongPlaying());
        }

        static void PrintWarning(string input, ErrorPos warning)
        {
            var posInfo = warning.PositionInfo;
            var posRange = posInfo.Range;

            int errorStartPos = posRange.Start.Value;
            int errorEndPos = posRange.End.Value;
            string lineStr = input.Split('\n')[posInfo.Line];
            string errorStr = lineStr[errorStartPos..errorEndPos];

            int startPos = Math.Max(0, errorStartPos - 10);
            int endPos = Math.Min(lineStr.Length, posRange.End.Value + 10);
            int errorStartOffset = errorStartPos - startPos;

            string warningMessage = $"{warning.Message}:\n";
            if (startPos != 0)
            {
                warningMessage += "...";
                errorStartOffset += 3;
            }
            warningMessage += lineStr[startPos..errorStartPos];

            warningMessage += $"<color=yellow>{lineStr[errorStartPos..errorEndPos]}</color>";

            warningMessage += lineStr[errorEndPos..endPos];
            if (endPos < lineStr.Length) warningMessage += "...";
            warningMessage += "\n";

            string caret = new string(' ', errorStartOffset) + new string('^', errorStr.Length);
            warningMessage += caret;

            Debug.LogWarning(warningMessage);
        }

        private List<Token> LexicalAnalysis(string input, out List<ErrorPos> warnings)
        {
            warnings = new List<ErrorPos>();
            List<Token> tokens = new List<Token>();
            int line = 0, position = 0;

            for (int i = 0; i < input.Length; i++, position++)
            {
                char c = input[i];

                switch (c)
                {
                    case '(':
                        {
                            int endBpm = input.IndexOf(')', i);
                            if (endBpm == -1)
                            {
                                warnings.Add(new ErrorPos("Unclosed BPM token", line, position, Math.Max(input.Length, 10)));
                                break;
                            }

                            int len = endBpm - i;
                            tokens.Add(new Token(TokenType.BPM, input.Substring(i + 1, len - 1), line, position, len));
                            i = endBpm;
                            position += len;
                        }
                        break;

                    case '{':
                        {
                            int endBeats = input.IndexOf('}', i);
                            if (endBeats == -1)
                            {
                                warnings.Add(new ErrorPos("Unclosed Beats token", line, position, Math.Max(input.Length, 10)));
                                break;
                            }

                            int len = endBeats - i;
                            tokens.Add(new Token(TokenType.Beats, input.Substring(i + 1, len - 1), line, position, len));
                            i = endBeats;
                            position += len;
                        }
                        break;

                    case ',':
                        tokens.Add(new Token(TokenType.Rest, ",", line, position));
                        break;

                    case '/':
                        tokens.Add(new Token(TokenType.Slash, "/", line, position));
                        break;

                    case '[':
                        {
                            int endSV = input.IndexOf(']', i);
                            if (endSV == -1)
                            {
                                warnings.Add(new ErrorPos("Unclosed SV token", line, position, Math.Max(input.Length, 10)));
                                break;
                            }

                            int len = endSV - i;
                            tokens.Add(new Token(TokenType.SV, input.Substring(i + 1, len - 1), line, position, len));
                            i = endSV;
                            position += len;
                        }
                        break;

                    case '\n':
                        tokens.Add(new Token(TokenType.NewLine, "\\n", line, position));
                        line++;
                        position = -1;
                        break;

                    case '#':
                        int endComment = input.IndexOf('\n', i);
                        string comment = endComment == -1
                            ? input[(i + 1)..]
                            : input.Substring(i + 1, endComment - i - 1);
                        tokens.Add(new Token(TokenType.Comment, comment, line, position));
                        i = endComment == -1 ? input.Length : endComment - 1;
                        break;

                    case ' ':
                        break;

                    default:
                        if (char.IsDigit(c))
                        {
                            if (i + 1 < input.Length && input[i + 1] == 'h')
                            {
                                int startBracket = input.IndexOf('[', i);
                                int endBracket = input.IndexOf(']', i);
                                if (startBracket == i + 2 && endBracket != -1)
                                {
                                    int len = endBracket - i + 1;
                                    tokens.Add(new Token(TokenType.HoldNote, input.Substring(i, len), line, position, len));
                                    i = endBracket;
                                    position += len - 1;
                                    continue;
                                }
                            }
                            tokens.Add(new Token(TokenType.Note, c.ToString(), line, position));
                        }
                        else
                            warnings.Add(new ErrorPos("Invalid character", line, position));
                        break;
                }
            }

            return tokens;
        }

        private List<Note> ParseTokens(List<Token> tokens, out List<ErrorPos> warnings)
        {
            warnings = new List<ErrorPos>();
            List<Note> notes = new List<Note>();
            HashSet<int> currentNotes = new HashSet<int>();

            decimal bpm = 120;
            int beatsPerMeasure = 4;
            decimal currentTime = (decimal)chartOffset;
            
            svEvents.Clear();
            svEvents.Add(new SVEvent(-1000f, 1.0f, 0f));

            Token? lastToken = null;

            foreach (var token in tokens)
            {
                if (token.Type != TokenType.Slash && token.Type != TokenType.Note) currentNotes.Clear();
                if (token.Type != TokenType.Note && lastToken?.Type == TokenType.Slash)
                    warnings.Add(new ErrorPos("Slash without note", token.Line, token.Range));

                switch (token.Type)
                {
                    case TokenType.BPM: // (120)
                        if (decimal.TryParse(token.Value, out decimal bpmValue))
                        {
                            if (bpmValue < 0) warnings.Add(new ErrorPos("BPM cannot be negative", token.Line, token.Range));
                            else if (bpmValue == 0) warnings.Add(new ErrorPos("BPM cannot be 0", token.Line, token.Range));
                            else bpm = bpmValue;
                        }
                        else warnings.Add(new ErrorPos("Invalid BPM", token.Line, token.Range));
                        break;

                    case TokenType.SV: // [1.5]
                        if (float.TryParse(token.Value, out float svValue))
                        {
                            float t = (float)currentTime;
                            if (t == chartOffset && svEvents.Count == 1)
                            {
                                svEvents[0] = new SVEvent(-1000f, svValue, 0f);
                            }
                            else
                            {
                                SVEvent lastEvent = svEvents[svEvents.Count - 1];
                                if (Mathf.Approximately(lastEvent.Time, t) && svEvents.Count > 1)
                                {
                                    svEvents.RemoveAt(svEvents.Count - 1);
                                    lastEvent = svEvents[svEvents.Count - 1];
                                }
                                float currentIntegral = lastEvent.Integral + (t - lastEvent.Time) * lastEvent.Multiplier;
                                svEvents.Add(new SVEvent(t, svValue, currentIntegral));
                            }
                        }
                        else warnings.Add(new ErrorPos("Invalid SV", token.Line, token.Range));
                        break;

                    case TokenType.Beats: // {<value>}
                        if (decimal.TryParse(token.Value, out decimal beatsPerMeasureValue))
                        {
                            if (beatsPerMeasureValue % 1 != 0)
                                warnings.Add(new ErrorPos("Invalid beats per measure, must be an integer", token.Line, token.Range));
                            else if (beatsPerMeasureValue < 1)
                                warnings.Add(new ErrorPos("Invalid beats per measure, must be at least 1", token.Line, token.Range));
                            else if (beatsPerMeasureValue == 0)
                                warnings.Add(new ErrorPos("Invalid beats per measure, cannot be 0", token.Line, token.Range));
                            else beatsPerMeasure = (int)beatsPerMeasureValue;
                        }
                        else
                            warnings.Add(new ErrorPos("Invalid beats per measure", token.Line, token.Range));
                        break;

                    case TokenType.Slash: // /
                        if (lastToken?.Type == TokenType.Slash)
                            warnings.Add(new ErrorPos("Double slash", token.Line, token.Range));
                        break;

                    case TokenType.Rest: // ,
                        currentTime += 60m / bpm * (4m / beatsPerMeasure);
                        break;

                    case TokenType.Note: // number
                        if (int.TryParse(token.Value, out int lane))
                        {
                            if (lane < 1 || lane > 4)
                                warnings.Add(new ErrorPos("Invalid note", token.Line, token.Range));
                            if (!currentNotes.Add(lane))
                                warnings.Add(new ErrorPos("Duplicate note", token.Line, token.Range));
                            else notes.Add(new Note(lane, (float)currentTime));
                        }
                        else
                            warnings.Add(new ErrorPos("Invalid note", token.Line, token.Range));
                        break;

                    case TokenType.HoldNote: // "1h[4:4]"
                        string holdStr = token.Value;
                        if (int.TryParse(holdStr[0].ToString(), out int hLane) && hLane >= 1 && hLane <= 4)
                        {
                            int colonIdx = holdStr.IndexOf(':');
                            if (colonIdx != -1)
                            {
                                string xStr = holdStr.Substring(3, colonIdx - 3);
                                string yStr = holdStr.Substring(colonIdx + 1, holdStr.Length - colonIdx - 2);
                                if (decimal.TryParse(xStr, out decimal xVal) && decimal.TryParse(yStr, out decimal yVal) && xVal > 0)
                                {
                                    if (!currentNotes.Add(hLane))
                                        warnings.Add(new ErrorPos("Duplicate note", token.Line, token.Range));
                                    else
                                    {
                                        float duration = (float)(yVal * (60m / bpm) * (4m / xVal));
                                        notes.Add(new Note(hLane, (float)currentTime, true, (float)currentTime + duration));
                                    }
                                }
                                else warnings.Add(new ErrorPos("Invalid hold parameters", token.Line, token.Range));
                            }
                            else warnings.Add(new ErrorPos("Invalid hold syntax", token.Line, token.Range));
                        }
                        else warnings.Add(new ErrorPos("Invalid hold lane", token.Line, token.Range));
                        break;

                    default:
                        // Ignore other token types
                        break;
                }

                lastToken = token;
            }

            return notes;
        }

        void Update()
        {
            if (!isPause)
            {
                UpdateSongTime();
                UpdateNotes();
            }

            UpdateMaxCombo();
            CheckSongEnd();
            UpdateScoreDisplay();
            HandleInput();
            HandleKeyPress();
        }

        void UpdateSongTime()
        {
            if (!audioPlayed)
            {
                songTime += Time.deltaTime;
                if (songTime >= 0f)
                {
                    BGM.GetComponent<AudioSource>().Play();
                    audioPlayed = true;
                }
            }
            else
            {
                songTime = BGM.GetComponent<AudioSource>().time;
            }
            songTimeOBJ.GetComponent<TextMeshProUGUI>().text = songTime.ToString("F2");
        }

        void UpdateNotes()
        {
            int index = 0;
            float approachTime = 2f / Mathf.Max(0.1f, speed);
            foreach (var note in notes)
            {
                index++;
                float timeToSpawn = note.Time;
                timeToSpawnOBJ.GetComponent<TextMeshProUGUI>().text = timeToSpawn.ToString();
                
                float integralDistToHit = GetIntegralFromZero(timeToSpawn) - GetIntegralFromZero(songTime);

                if (integralDistToHit <= approachTime && !noteSpawned[note])
                {
                    Debug.Log("Spawning note at lane: " + note.Lane);
                    if (note.IsHold) CreateHoldNote(note.Lane, note.Time, note.EndTime);
                    else CreateNote(note.Lane, note.Time);
                    noteSpawned[note] = true;
                }
                else if (songTime >= timeToSpawn && !noteSpawned[note])
                {
                    // 容錯機制，確保音符能夠準時生成
                    Debug.LogWarning("Late spawning note at lane: " + note.Lane);
                    if (note.IsHold) CreateHoldNote(note.Lane, note.Time, note.EndTime);
                    else CreateNote(note.Lane, note.Time);
                    noteSpawned[note] = true;
                }
            }

            MoveNotes(_Note_1_List, _Note_1_Times, _Note_1_StartPos, TargetNote_1);
            MoveNotes(_Note_2_List, _Note_2_Times, _Note_2_StartPos, TargetNote_2);
            MoveNotes(_Note_3_List, _Note_3_Times, _Note_3_StartPos, TargetNote_3);
            MoveNotes(_Note_4_List, _Note_4_Times, _Note_4_StartPos, TargetNote_4);

            MoveHoldNotes(_HoldNote_1_List, TargetNote_1);
            MoveHoldNotes(_HoldNote_2_List, TargetNote_2);
            MoveHoldNotes(_HoldNote_3_List, TargetNote_3);
            MoveHoldNotes(_HoldNote_4_List, TargetNote_4);
        }

        void UpdateMaxCombo()
        {
            if (combo > maxcombo)
            {
                maxcombo = combo;
            }
        }

        void CheckSongEnd()
        {
            if (BGM.GetComponent<AudioSource>().time >= BGM.GetComponent<AudioSource>().clip.length)
            {
                ShowResult();
            }
        }

        void ShowResult()
        {
            result.SetActive(true);
            result_Score.GetComponent<TextMeshProUGUI>().text = score.ToString();
            result_Perfect.GetComponent<TextMeshProUGUI>().text = countPerfect.ToString();
            result_Great.GetComponent<TextMeshProUGUI>().text = countGreat.ToString();
            result_Miss.GetComponent<TextMeshProUGUI>().text = countMiss.ToString();
            result_Combo.GetComponent<TextMeshProUGUI>().text = "Combo: " + maxcombo.ToString();
            UpdateRank();
            UpdateAPFC();
        }

        void UpdateRank()
        {
            if (score == 1000000)
            {
                result_Rank.GetComponent<TextMeshProUGUI>().text = "SSS+";
                result_Rank.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.5f, 0f);
            }
            else if (score >= 990000)
            {
                result_Rank.GetComponent<TextMeshProUGUI>().text = "SSS";
                result_Rank.GetComponent<TextMeshProUGUI>().color = Color.yellow;
            }
            else if (score >= 980000)
            {
                result_Rank.GetComponent<TextMeshProUGUI>().text = "SS";
                result_Rank.GetComponent<TextMeshProUGUI>().color = Color.yellow;
            }
            else if (score >= 970000)
            {
                result_Rank.GetComponent<TextMeshProUGUI>().text = "S";
                result_Rank.GetComponent<TextMeshProUGUI>().color = Color.yellow;
            }
            else if (score >= 950000)
            {
                result_Rank.GetComponent<TextMeshProUGUI>().text = "A";
                result_Rank.GetComponent<TextMeshProUGUI>().color = Color.green;
            }
            else if (score >= 900000)
            {
                result_Rank.GetComponent<TextMeshProUGUI>().text = "B";
                result_Rank.GetComponent<TextMeshProUGUI>().color = Color.blue;
            }
            else if (score >= 800000)
            {
                result_Rank.GetComponent<TextMeshProUGUI>().text = "C";
                result_Rank.GetComponent<TextMeshProUGUI>().color = Color.red;
            }
            else
            {
                result_Rank.GetComponent<TextMeshProUGUI>().text = "D";
                result_Rank.GetComponent<TextMeshProUGUI>().color = Color.red;
            }
        }

        void UpdateAPFC()
        {
            if (countGreat == 0 && countMiss == 0)
            {
                result_APFC.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.5f, 0f);
                result_APFC.GetComponent<TextMeshProUGUI>().text = "AP";
            }
            else if (countMiss == 0)
            {
                result_APFC.GetComponent<TextMeshProUGUI>().color = Color.green;
                result_APFC.GetComponent<TextMeshProUGUI>().text = "FC";
            }
            else
            {
                result_APFC.GetComponent<TextMeshProUGUI>().text = "";
            }
        }

        void UpdateScoreDisplay()
        {
            countPerfectOBJ.GetComponent<TextMeshProUGUI>().text = "Perfect: " + countPerfect;
            countGreatOBJ.GetComponent<TextMeshProUGUI>().text = "Great: " + countGreat;
            countMissOBJ.GetComponent<TextMeshProUGUI>().text = "Miss: " + countMiss;
            countComboOBJ.GetComponent<TextMeshProUGUI>().text = combo.ToString();

            countComboOBJ.SetActive(combo != 0);

            if (displayedScore < score)
            {
                displayedScore += Mathf.CeilToInt((score - displayedScore) * 0.1f);
                if (displayedScore > score)
                {
                    displayedScore = score;
                }
            }
            scoreOBJ.GetComponent<TextMeshProUGUI>().text = displayedScore.ToString("D7");
        }

        void HandleInput()
        {
            if (Input.GetKeyDown(keys[4]))
            {
                pause.SetActive(!pause.activeSelf);
                isPause = !isPause;
                if (isPause)
                {
                    BGM.GetComponent<AudioSource>().Pause();
                }
                else
                {
                    if (audioPlayed)
                    {
                        BGM.GetComponent<AudioSource>().UnPause();
                    }
                }
            }
        }

        void HandleKeyPress()
        {
            for (int i = 0; i < keys.Length; i++)
            {
                bool isKeyDown = Input.GetKeyDown(keys[i]);
                bool isKey = Input.GetKey(keys[i]);

                if (isKeyDown)
                {
                    ProcessKeyDown(i + 1);
                }
                
                ProcessKeyHold(i + 1, isKey);
            }
        }

        IEnumerator StartSongPlaying()
        {
            yield return null;
            playing = true;
            isPause = false;
        }


        void MoveNotes(List<GameObject> noteList, List<float> timeList, List<Vector3> startPosList, GameObject target)
        {
            float approachTime = 2f / Mathf.Max(0.1f, speed);
            for (int i = noteList.Count - 1; i >= 0; i--)
            {
                if (noteList[i] != null && target != null)
                {
                    float hitTime = timeList[i];
                    float integralDistToHit = GetIntegralFromZero(hitTime) - GetIntegralFromZero(songTime);
                    float progress = 1.0f - (integralDistToHit / approachTime);

                    noteList[i].transform.position = Vector3.LerpUnclamped(startPosList[i], target.transform.position, progress);
                    
                    JudgeTime.GetComponent<TextMeshProUGUI>().text = Mathf.Round(noteList[i].transform.position.y) + "/" + Mathf.Round(target.transform.position.y);
                    // Destroy note if it has passed the miss window
                    if (songTime - hitTime > missWindow / 1000f)
                    {
                        Destroy(noteList[i]);
                        noteList.RemoveAt(i);
                        timeList.RemoveAt(i);
                        startPosList.RemoveAt(i);
                        DisplayJudgeResult(Judge_Miss);
                        countMiss++; // 增加 miss 計數
                        combo = 0; // 重置 combo 計數

                    }
                }
            }
        }

        IEnumerator JudgeReset(GameObject judge)
        {
            yield return new WaitForSeconds(0.5f);
            judge.SetActive(false);
        }

        void CreateNote(int lane, float hitTime)
        {
            //Debug.Log(lane);
            GameObject Canvas = GameObject.FindGameObjectWithTag("Canvas");
            GameObject TagNote = GameObject.FindGameObjectWithTag("TagNote");
            GameObject newNote = null;

            switch (lane)
            {
                case 1:
                    newNote = Instantiate(Note_1);
                    _Note_1_List.Add(newNote);
                    _Note_1_Times.Add(hitTime); // Store the target hit time
                    break;
                case 2:
                    newNote = Instantiate(Note_2);
                    _Note_2_List.Add(newNote);
                    _Note_2_Times.Add(hitTime);
                    break;
                case 3:
                    newNote = Instantiate(Note_3);
                    _Note_3_List.Add(newNote);
                    _Note_3_Times.Add(hitTime);
                    break;
                case 4:
                    newNote = Instantiate(Note_4);
                    _Note_4_List.Add(newNote);
                    _Note_4_Times.Add(hitTime);
                    break;
            }

            if (newNote != null)
            {
                newNote.SetActive(true);
                newNote.transform.SetParent(TagNote.transform, false);
                
                switch (lane)
                {
                    case 1: _Note_1_StartPos.Add(newNote.transform.position); break;
                    case 2: _Note_2_StartPos.Add(newNote.transform.position); break;
                    case 3: _Note_3_StartPos.Add(newNote.transform.position); break;
                    case 4: _Note_4_StartPos.Add(newNote.transform.position); break;
                }
            }
        }

        void ProcessKeyDown(int lane)
        {
            List<GameObject> tapNotes = null;
            List<float> tapTimes = null;
            List<HoldNoteObj> holdNotes = null;
            List<Vector3> tapStartPos = null;

            switch (lane)
            {
                case 1: tapNotes = _Note_1_List; tapTimes = _Note_1_Times; tapStartPos = _Note_1_StartPos; holdNotes = _HoldNote_1_List; break;
                case 2: tapNotes = _Note_2_List; tapTimes = _Note_2_Times; tapStartPos = _Note_2_StartPos; holdNotes = _HoldNote_2_List; break;
                case 3: tapNotes = _Note_3_List; tapTimes = _Note_3_Times; tapStartPos = _Note_3_StartPos; holdNotes = _HoldNote_3_List; break;
                case 4: tapNotes = _Note_4_List; tapTimes = _Note_4_Times; tapStartPos = _Note_4_StartPos; holdNotes = _HoldNote_4_List; break;
            }

            float earliestTap = float.MaxValue;
            if (tapTimes != null && tapTimes.Count > 0) earliestTap = tapTimes[0];

            float earliestHold = float.MaxValue;
            HoldNoteObj targetHold = null;
            if (holdNotes != null)
            {
                foreach (var h in holdNotes)
                {
                    if (!h.HeadHit && !h.IsDead)
                    {
                        earliestHold = h.HitTime;
                        targetHold = h;
                        break;
                    }
                }
            }

            if (earliestTap <= earliestHold && earliestTap != float.MaxValue)
            {
                bool hit = TryHitTap(tapNotes, tapTimes, tapStartPos);
                if (!hit && targetHold != null) TryHitHoldHead(targetHold);
            }
            else if (targetHold != null)
            {
                bool hit = TryHitHoldHead(targetHold);
                if (!hit && earliestTap != float.MaxValue) TryHitTap(tapNotes, tapTimes, tapStartPos);
            }
        }

        bool TryHitTap(List<GameObject> currentNoteList, List<float> currentNoteTimes, List<Vector3> currentStartPosList)
        {
            if (currentNoteList != null && currentNoteList.Count > 0)
            {
                GameObject noteObject = currentNoteList[0];
                float targetTime = currentNoteTimes[0];
                float timeDiffMS = Mathf.Abs(songTime - targetTime) * 1000f;

                if (timeDiffMS <= perfectWindow)
                {
                    DisplayJudgeResult(Judge_Perfect);
                    countPerfect++;
                    combo++;
                    score += Mathf.CeilToInt((float)maxScore / totalNotes);
                }
                else if (timeDiffMS <= greatWindow)
                {
                    DisplayJudgeResult(Judge_Great);
                    countGreat++;
                    combo++;
                    score += Mathf.CeilToInt((float)maxScore / totalNotes * 0.6f);
                }
                else if (timeDiffMS <= missWindow)
                {
                    DisplayJudgeResult(Judge_Miss);
                    countMiss++;
                    combo = 0;
                }
                else
                {
                    JudgeTime.GetComponent<TextMeshProUGUI>().text = timeDiffMS.ToString("F0") + "ms";
                    return false;
                }

                JudgeTime.GetComponent<TextMeshProUGUI>().text = timeDiffMS.ToString("F0") + "ms";
                JudgeAudio.GetComponent<AudioSource>().Play();
                Destroy(noteObject);
                currentNoteList.RemoveAt(0);
                currentNoteTimes.RemoveAt(0);
                currentStartPosList.RemoveAt(0);
                
                if (score > maxScore) score = maxScore;
                return true;
            }
            return false;
        }

        bool TryHitHoldHead(HoldNoteObj hold)
        {
            float timeDiffMS = Mathf.Abs(songTime - hold.HitTime) * 1000f;
            if (timeDiffMS <= greatWindow)
            {
                hold.HeadHit = true;
                if (hold.Head != null) hold.Head.SetActive(false); // Hide head
                
                if (timeDiffMS <= perfectWindow)
                {
                    DisplayJudgeResult(Judge_Perfect);
                    countPerfect++;
                }
                else
                {
                    DisplayJudgeResult(Judge_Great);
                    countGreat++;
                }
                combo++;
                score += Mathf.CeilToInt((float)maxScore / totalNotes);
                JudgeTime.GetComponent<TextMeshProUGUI>().text = timeDiffMS.ToString("F0") + "ms";
                JudgeAudio.GetComponent<AudioSource>().Play();
                if (score > maxScore) score = maxScore;
                return true;
            }
            return false;
        }

        void ProcessKeyHold(int lane, bool isKey)
        {
            List<HoldNoteObj> currentList = null;
            switch (lane)
            {
                case 1: currentList = _HoldNote_1_List; break;
                case 2: currentList = _HoldNote_2_List; break;
                case 3: currentList = _HoldNote_3_List; break;
                case 4: currentList = _HoldNote_4_List; break;
            }

            if (currentList == null) return;

            for (int i = 0; i < currentList.Count; i++)
            {
                HoldNoteObj hold = currentList[i];
                if (hold.IsDead) continue;

                if (hold.HeadHit)
                {
                    if (!isKey && songTime < hold.EndTime)
                    {
                        // Released early -> Miss
                        hold.IsDead = true;
                        TurnHoldBlack(hold);
                        DisplayJudgeResult(Judge_Miss);
                        countMiss++;
                        combo = 0;
                        UpdateScoreDisplay();
                        continue;
                    }
                    
                    if (isKey && songTime >= hold.EndTime)
                    {
                        // Successfully held to end
                        if (hold.Head != null) Destroy(hold.Head);
                        if (hold.Tail != null) Destroy(hold.Tail);
                        if (hold.Body != null) Destroy(hold.Body);
                        currentList.RemoveAt(i);
                        
                        DisplayJudgeResult(Judge_Perfect);
                        countPerfect++;
                        combo++;
                        score += Mathf.CeilToInt((float)maxScore / totalNotes);
                        UpdateScoreDisplay();
                        JudgeAudio.GetComponent<AudioSource>().Play();
                        if (score > maxScore) score = maxScore;
                        i--;
                        continue;
                    }
                }
            }
        }

        void TurnHoldBlack(HoldNoteObj hold)
        {
            if (hold.Head != null)
            {
                var img = hold.Head.GetComponent<Image>();
                if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, img.color.a);
                else
                {
                    var sr = hold.Head.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(0.3f, 0.3f, 0.3f, sr.color.a);
                }
            }
            if (hold.Tail != null)
            {
                var img = hold.Tail.GetComponent<Image>();
                if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, img.color.a);
                else
                {
                    var sr = hold.Tail.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(0.3f, 0.3f, 0.3f, sr.color.a);
                }
            }
            if (hold.Body != null)
            {
                var img = hold.Body.GetComponent<Image>();
                if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, img.color.a);
                else
                {
                    var sr = hold.Body.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = new Color(0.3f, 0.3f, 0.3f, sr.color.a);
                }
            }
        }

        void CreateHoldNote(int lane, float hitTime, float endTime)
        {
            GameObject TagNote = GameObject.FindGameObjectWithTag("TagNote");
            GameObject baseNotePref = null;
            List<HoldNoteObj> currentList = null;

            switch (lane)
            {
                case 1: baseNotePref = Note_1; currentList = _HoldNote_1_List; break;
                case 2: baseNotePref = Note_2; currentList = _HoldNote_2_List; break;
                case 3: baseNotePref = Note_3; currentList = _HoldNote_3_List; break;
                case 4: baseNotePref = Note_4; currentList = _HoldNote_4_List; break;
            }

            if (baseNotePref != null && currentList != null)
            {
                HoldNoteObj holdObj = new HoldNoteObj();
                holdObj.HitTime = hitTime;
                holdObj.EndTime = endTime;
                holdObj.HeadHit = false;

                holdObj.Head = Instantiate(baseNotePref);
                holdObj.Head.SetActive(true);
                holdObj.Head.transform.SetParent(TagNote.transform, false);
                holdObj.HeadStartPos = holdObj.Head.transform.position;

                holdObj.Tail = Instantiate(baseNotePref);
                holdObj.Tail.SetActive(true);
                holdObj.Tail.transform.SetParent(TagNote.transform, false);
                holdObj.TailStartPos = holdObj.Tail.transform.position;

                holdObj.Body = new GameObject("HoldBody");
                holdObj.Body.transform.SetParent(TagNote.transform, false);
                
                Image headImg = holdObj.Head.GetComponent<Image>();
                if (headImg != null)
                {
                    Image bodyImg = holdObj.Body.AddComponent<Image>();
                    bodyImg.sprite = headImg.sprite;
                    bodyImg.color = new Color(1f, 1f, 1f, 0.5f);
                    holdObj.Body.transform.SetSiblingIndex(0);
                    RectTransform rect = holdObj.Body.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(headImg.rectTransform.sizeDelta.x * 0.8f, rect.sizeDelta.y);
                }
                else 
                {
                    SpriteRenderer headSr = holdObj.Head.GetComponent<SpriteRenderer>();
                    if (headSr != null)
                    {
                        SpriteRenderer bodySr = holdObj.Body.AddComponent<SpriteRenderer>();
                        bodySr.sprite = headSr.sprite;
                        bodySr.color = new Color(1f, 1f, 1f, 0.5f);
                        bodySr.sortingOrder = headSr.sortingOrder - 1;
                        holdObj.Body.transform.localScale = new Vector3(0.8f, 1f, 1f);
                    }
                }

                currentList.Add(holdObj);
            }
        }

        void MoveHoldNotes(List<HoldNoteObj> noteList, GameObject target)
        {
            float approachTime = 2f / Mathf.Max(0.1f, speed);
            for (int i = noteList.Count - 1; i >= 0; i--)
            {
                HoldNoteObj hold = noteList[i];

                if (songTime - hold.EndTime > missWindow / 1000f)
                {
                    if (hold.Head != null) Destroy(hold.Head);
                    if (hold.Tail != null) Destroy(hold.Tail);
                    if (hold.Body != null) Destroy(hold.Body);
                    
                    if (hold.IsDead && !hold.HeadHit)
                    {
                        DisplayJudgeResult(Judge_Miss);
                        countMiss++;
                        combo = 0;
                        UpdateScoreDisplay();
                    }

                    noteList.RemoveAt(i);
                    continue;
                }

                if (target != null)
                {
                    if (!hold.HeadHit)
                    {
                        float headIntegralDist = GetIntegralFromZero(hold.HitTime) - GetIntegralFromZero(songTime);
                        float headProgress = 1.0f - (headIntegralDist / approachTime);
                        if (hold.Head != null)
                            hold.Head.transform.position = Vector3.LerpUnclamped(hold.HeadStartPos, target.transform.position, headProgress);
                        
                        if (!hold.IsDead && songTime - hold.HitTime > missWindow / 1000f)
                        {
                            hold.IsDead = true;
                            TurnHoldBlack(hold);
                            DisplayJudgeResult(Judge_Miss);
                            countMiss++;
                            combo = 0;
                            UpdateScoreDisplay();
                            continue;
                        }
                    }
                    else if (hold.Head != null)
                    {
                        hold.Head.transform.position = target.transform.position;
                    }

                    float tailIntegralDist = GetIntegralFromZero(hold.EndTime) - GetIntegralFromZero(songTime);
                    float tailProgress = 1.0f - (tailIntegralDist / approachTime);
                    if (hold.Tail != null)
                    {
                        hold.Tail.transform.position = Vector3.LerpUnclamped(hold.TailStartPos, target.transform.position, tailProgress);
                    }

                    if (hold.Body != null && hold.Head != null && hold.Tail != null)
                    {
                        Vector3 headPos = hold.Head.transform.position;
                        Vector3 tailPos = hold.Tail.transform.position;
                        
                        Image bodyImg = hold.Body.GetComponent<Image>();
                        if (bodyImg != null)
                        {
                            RectTransform bodyRect = hold.Body.GetComponent<RectTransform>();
                            bodyRect.position = (headPos + tailPos) / 2f;
                            float height = Mathf.Abs(headPos.y - tailPos.y) / bodyRect.lossyScale.y;
                            bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, height);
                        }
                        else
                        {
                            hold.Body.transform.position = (headPos + tailPos) / 2f;
                            float dist = Mathf.Abs(headPos.y - tailPos.y);
                            SpriteRenderer headSr = hold.Head.GetComponent<SpriteRenderer>();
                            if (headSr != null && headSr.sprite != null)
                            {
                                float spriteHeight = headSr.sprite.bounds.size.y;
                                hold.Body.transform.localScale = new Vector3(0.8f, dist / spriteHeight, 1f);
                            }
                        }
                    }
                }
            }
        }

        void DisplayJudgeResult(Sprite judgmentSprite)
        {
            Judge.GetComponent<Image>().sprite = judgmentSprite;
            Judge.SetActive(true);

            // Reset the coroutine if it's already running
            if (judgeResetCoroutine != null)
            {
                StopCoroutine(judgeResetCoroutine);
            }

            judgeResetCoroutine = StartCoroutine(JudgeReset(Judge));
        }

        public void RemuseButton()
        {
            isPause = false;
            pause.SetActive(false);
            if (audioPlayed)
            {
                BGM.GetComponent<AudioSource>().UnPause();
            }
        }

        public void RestartButton()
        {
            SceneManager.LoadScene("SongPlaying");
        }

        public void ExitButton()
        {
            SceneManager.LoadScene("SongSelect");
        }
    }
}
