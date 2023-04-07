using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Whisper.Samples
{
    public class MicrophoneDemo : MonoBehaviour
    {
        public WhisperManager whisper;

        [Header("Mic settings")] 
        public int maxLengthSec = 30;
        public int frequency = 16000;
        public bool echo = true;

        [Header("UI")] 
        public Button button;
        public Text buttonText;
        public Text outputText;
        public Text timeText;
        public Dropdown languageDropdown;

        [SerializeField]
        private bool isIntervalRun = true;
        [SerializeField]
        private float interval = 3f;

        private float elapsedTime = 0f;

        private float _recordStart;
        private bool _isRecording;
        private AudioClip _clip;

        //音声データ
        private List<float> voiceDataList = new List<float>();

        private bool runningTranscribe = false;

        private void Awake()
        {
            button.onClick.AddListener(OnButtonPressed);
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
        }

        private void Update()
        {
            if (!_isRecording)
                return;

            var timePassed = Time.realtimeSinceStartup - _recordStart;
            if (timePassed > maxLengthSec)
                StopRecord();

            if(_isRecording)
            {
                elapsedTime += Time.deltaTime;

                if(isIntervalRun
                    && elapsedTime > interval)
                {
                    elapsedTime = 0f;

                    AddRecord();
                    UpdateText();
                }
            }
        }

        public void OnButtonPressed()
        {
            if (!_isRecording)
                StartRecord();
            else
                StopRecord();

            if (buttonText)
                buttonText.text = _isRecording ? "Stop" : "Record";
        }

        private void OnLanguageChanged(int ind)
        {
            var opt = languageDropdown.options[ind];
            whisper.SetLanguage(opt.text);
        }

        public void StartRecord()
        {
            if (_isRecording)
                return;

            _recordStart = Time.realtimeSinceStartup;
            _clip = Microphone.Start(null, false, maxLengthSec, frequency);
            _isRecording = true;
        }

        private void AddRecord()
        {
            //音声データを追加
            var data = GetTrimmedData();

            //毎回一から取得しておりAddする必要はなさそうなので、暫定対応として毎回Clearする
            voiceDataList.Clear();

            voiceDataList.AddRange(data);
        }

        private void UpdateText()
        {
            Transcribe(voiceDataList.ToArray());
        }

        public void StopRecord()
        {
            if (!_isRecording)
                return;

            //音声データを追加
            var data = GetTrimmedData();
            AddRecord();

            if (echo)
            {
                var echoClip = AudioClip.Create("echo", data.Length,
                    _clip.channels, _clip.frequency, false);
                echoClip.SetData(data, 0);
                AudioSource.PlayClipAtPoint(echoClip, Vector3.zero);
            }

            Microphone.End(null);
            _isRecording = false;

            //Transcribe(data);
            //UpdateText();
        }

        private float[] GetTrimmedData()
        {
            // get microphone samples and current position
            var pos = Microphone.GetPosition(null);
            var origData = new float[_clip.samples * _clip.channels];
            _clip.GetData(origData, 0);

            // check if mic just reached audio buffer end
            if (pos == 0)
                return origData;

            // looks like we need to trim it by pos
            var trimData = new float[pos];
            Array.Copy(origData, trimData, pos);
            return trimData;
        }

        private async void Transcribe(float[] data)
        {
            if (runningTranscribe) return;
            runningTranscribe = true;

            var sw = new Stopwatch();
            sw.Start();
            
            var res = await whisper.GetTextAsync(data, _clip.frequency, _clip.channels);

            timeText.text = $"Time: {sw.ElapsedMilliseconds} ms";
            if (res == null)
                return;

            SetText(res.Result);

            runningTranscribe = false;
        }

        private void SetText(string str)
        {
            StringBuilder sb = new StringBuilder();
            for(int i=0; i<str.Length; i++)
            {
                sb.Append(str[i]);
                if (i % 15 == 0 && i>=1) sb.AppendLine();
            }
            outputText.text = sb.ToString();
        }
    }
}