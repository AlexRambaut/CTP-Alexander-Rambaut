using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Microsoft.Speech.Recognition;
using Microsoft.Speech.AudioFormat;
using System.IO;
using NAudio.Wave;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Incanter
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        #region Dictionary
        private Dictionary<string, int> firstWord = new Dictionary<string, int>
        {
            { "seir", 1 },
            { "oss", 2 },
            { "nox", 3 },
        };
        #endregion

        Texture2D backgroundTexture;
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        ParticleEngine particleEngine;
        SpeechRecognitionEngine speechRecognizer;
        KinectSensor Kinect;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            OpenKinect(KinectSensor.KinectSensors.FirstOrDefault(k => k.Status == KinectStatus.Connected));
        }

        //Starting the Kinect
        private void OpenKinect(KinectSensor kinectSensor)
        {
            if (kinectSensor != null)
            {
                Kinect = kinectSensor;
                Kinect.Start();
                InitializeSpeechRecognition();
            }
        }

        //Make sure the speech recognition engine is present
        private static RecognizerInfo GetKinectRecognizer()
        {
            Func<RecognizerInfo, bool> matchingFunc = r =>
            {
                string value;
                r.AdditionalInfo.TryGetValue("Kinect", out value);
                return "True".Equals(value, StringComparison.InvariantCultureIgnoreCase)
                    && "en-US".Equals(r.Culture.Name, StringComparison.InvariantCultureIgnoreCase);
            };
            return SpeechRecognitionEngine.InstalledRecognizers().Where(matchingFunc).FirstOrDefault();
        }

        //Start up speech recognition
        private void InitializeSpeechRecognition()
        {
            RecognizerInfo ri = GetKinectRecognizer();
            if (ri == null)
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            try
            {
                speechRecognizer = new SpeechRecognitionEngine(ri.Id);
            }
            catch
            {
                MessageBox.Show(
                    @"There was a problem initializing Speech Recognition.
Ensure you have the Microsoft Speech SDK installed and configured.",
                    "Failed to load Speech SDK",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            //Check for a starting word in the dictionary
            var firstword = new Choices();
            foreach (string value in firstWord.Keys)
                firstword.Add(value);

            //Spell structure 1
            GrammarBuilder firstword1 = new Choices(new string[] { "seir" });
            firstword1.Append("surita");
            firstword1.Append("fimm");
            firstword1.Append("groenn");
            firstword1.Append("vindr");

            //Spell structure 2
            GrammarBuilder firstword2 = new Choices(new string[] { "oss" });
            firstword2.Append("naeo");
            firstword2.Append("fyor");
            firstword2.Append("regin");
            firstword2.Append("tinada");
            firstword2.Append("varindo");
            firstword2.Append("yotsun");

            //Exit spell
            GrammarBuilder exit = new Choices(new string[] { "nox" });
            exit.Append("eterna");

            //Choose between spells
            Choices first = new Choices();
            first.Add(new Choices(new GrammarBuilder[] { firstword1, firstword2, exit }));

            var gb = new GrammarBuilder();
            gb.Culture = ri.Culture;
            gb.Append(first);

            //Create new grammar that includes custom dictionary
            var g = new Grammar(gb);

            speechRecognizer.LoadGrammar(g);
            speechRecognizer.SpeechRecognized += speechRecognizer_SpeechRecognized;
            speechRecognizer.SpeechHypothesized += speechRecognizer_SpeechHypothesized;
            speechRecognizer.SpeechRecognitionRejected += speechRecognizer_SpeechRecognitionRejected;

            if (Kinect == null || speechRecognizer == null)
                return;

            var audioSource = this.Kinect.AudioSource;
            audioSource.BeamAngleMode = BeamAngleMode.Adaptive;
            var kinectStream = audioSource.Start();

            //Listen
            speechRecognizer.SetInputToAudioStream(
                    kinectStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
            speechRecognizer.RecognizeAsync(RecognizeMode.Multiple);
        }

        //Recognised a word
        void speechRecognizer_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            Console.WriteLine("Rejected: " + e.Result.Text);
            //particleEngine.amount = 0;
        }

        void speechRecognizer_SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            Console.WriteLine("Hypothesized: " + e.Result.Text);
        }

        void speechRecognizer_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence < 0.70)
            {
                Console.WriteLine("Rejected: " + e.Result.Text + ", " + e.Result.Confidence);
                if (e.Result.Alternates.Count > 0)
                {
                    // Print the alternatives
                    Console.WriteLine("Alternates available: " + e.Result.Alternates.Count);
                    foreach (RecognizedPhrase alternate in e.Result.Alternates)
                    {
                        Console.WriteLine("Alternate: " + alternate.Text + ", " + alternate.Confidence);
                    }
                }
                return;
            }
            Console.WriteLine("Recognized: " + e.Result.Text + ", " + e.Result.Confidence);
            particleEngine.amount = 80;
            if (e.Result.Text == "nox eterna")
            {
                //this.Exit();
                particleEngine.amount = 0;
            }
        }

        //Create window
        protected override void Initialize()
        {
            base.Initialize();
            graphics.PreferredBackBufferWidth = 800;
            graphics.PreferredBackBufferHeight = 600;
            graphics.IsFullScreen = false;
            graphics.ApplyChanges();
            Window.Title = "Incanter";
        }

        //Load particles
        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            backgroundTexture = Content.Load<Texture2D>("background");
            List<Texture2D> textures = new List<Texture2D>();
            textures.Add(Content.Load<Texture2D>("circle"));
            particleEngine = new ParticleEngine(textures, new Vector2(400, 240));
            
        }

        protected override void UnloadContent()
        {
        }

        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            particleEngine.EmitterLocation = new Vector2(Microsoft.Xna.Framework.Input.Mouse.GetState().X, Microsoft.Xna.Framework.Input.Mouse.GetState().Y);
            particleEngine.Update();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);

            particleEngine.Draw(spriteBatch);

            base.Draw(gameTime);
        }

        private void DrawScenery()
        {
            
        }
    }
}
