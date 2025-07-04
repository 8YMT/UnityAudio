using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class EditorManger : MonoBehaviour
{
    public LandingMenu landingMenu; // Assign this in the Inspector

    // Section navigation
    public Button nextSectionButton;
    public Button prevSectionButton;
    public Text sectionIndexText;

    // Completion UI
    public Image completionImage;    // Assign in Inspector
    public Image editingImage;       // Assign in Inspector

    // Scene Lights UI
    public Toggle scenePulsingToggle;
    public Toggle[] scenePulsingSequenceToggles; // size 11
    public Button scenePulsingPrevButton;
    public Button scenePulsingNextButton;
    public Text scenePulsingIndexText;
    public Toggle sceneRotationToggle;
    public Toggle[] sceneRotationSequenceToggles; // size 11
    public InputField[] sceneOnStartingAngleInputs; // size 2
    public InputField[] sceneOnAngleInputs; // size 2
    public InputField[] sceneOffStartingAngleInputs; // size 2
    public InputField[] sceneOffAngleInputs; // size 2
    public Toggle[] sceneDelayedToggles; // size 2
    public InputField sceneColourInput;

    // Ceiling Lights UI
    public Toggle ceilingPulsingToggle;
    public Toggle[] ceilingPulsingSequenceToggles; // size 8
    public Button ceilingPulsingPrevButton;
    public Button ceilingPulsingNextButton;
    public Text ceilingPulsingIndexText;
    public Toggle ceilingRotationToggle;
    public Toggle[] ceilingRotationSequenceToggles; // size 8
    public InputField[] ceilingOnStartingAngleInputs; // size 2
    public InputField[] ceilingOnAngleInputs; // size 2
    public InputField[] ceilingOffStartingAngleInputs; // size 2
    public InputField[] ceilingOffAngleInputs; // size 2
    public Toggle[] ceilingDelayedToggles; // size 2
    public InputField ceilingColourInput;

    // Second Floor Lights UI
    public Toggle secondPulsingToggle;
    public Toggle[] secondPulsingSequenceToggles; // size 12
    public Button secondPulsingPrevButton;
    public Button secondPulsingNextButton;
    public Text secondPulsingIndexText;
    public Toggle secondRotationToggle;
    public Toggle[] secondRotationSequenceToggles; // size 12
    public InputField[] secondOnStartingAngleInputs; // size 2
    public InputField[] secondOnAngleInputs; // size 2
    public InputField[] secondOffStartingAngleInputs; // size 2
    public InputField[] secondOffAngleInputs; // size 2
    public Toggle[] secondDelayedToggles; // size 2
    public InputField secondColourInput;

    [System.Serializable]
    public class Section
    {
        public string Name;
        public SceneLightsData SceneLights = new SceneLightsData();
        public CeilingLightsData CeilingLights = new CeilingLightsData();
        public SecondFloorLightsData SecondFloorLights = new SecondFloorLightsData();
    }

    [System.Serializable]
    public class SceneLightsData
    {
        public bool Pulsing;
        public bool[][] PulsingSequence = new bool[4][];
        public bool Rotation;
        public bool[] RotationSequence = new bool[11];
        public float[] OnStartingAngle = new float[2];
        public float[] OnAngle = new float[2];
        public float[] OffStartingAngle = new float[2];
        public float[] OffAngle = new float[2];
        public bool[] Delayed = new bool[2];
        public string Colour;

        public void Init()
        {
            for (int i = 0; i < 4; i++)
                PulsingSequence[i] = new bool[11];
            for (int i = 0; i < 11; i++)
                RotationSequence[i] = false;
            for (int i = 0; i < 2; i++)
            {
                OnStartingAngle[i] = 0;
                OnAngle[i] = 0;
                OffStartingAngle[i] = 0;
                OffAngle[i] = 0;
                Delayed[i] = false;
            }
            Colour = "";
        }
    }

    [System.Serializable]
    public class CeilingLightsData
    {
        public bool Pulsing;
        public bool[][] PulsingSequence = new bool[4][];
        public bool Rotation;
        public bool[] RotationSequence = new bool[8];
        public float[] OnStartingAngle = new float[2];
        public float[] OnAngle = new float[2];
        public float[] OffStartingAngle = new float[2];
        public float[] OffAngle = new float[2];
        public bool[] Delayed = new bool[2];
        public string Colour;

        public void Init()
        {
            for (int i = 0; i < 4; i++)
                PulsingSequence[i] = new bool[8];
            for (int i = 0; i < 8; i++)
                RotationSequence[i] = false;
            for (int i = 0; i < 2; i++)
            {
                OnStartingAngle[i] = 0;
                OnAngle[i] = 0;
                OffStartingAngle[i] = 0;
                OffAngle[i] = 0;
                Delayed[i] = false;
            }
            Colour = "";
        }
    }

    [System.Serializable]
    public class SecondFloorLightsData
    {
        public bool Pulsing;
        public bool[][] PulsingSequence = new bool[4][];
        public bool Rotation;
        public bool[] RotationSequence = new bool[12];
        public float[] OnStartingAngle = new float[2];
        public float[] OnAngle = new float[2];
        public float[] OffStartingAngle = new float[2];
        public float[] OffAngle = new float[2];
        public bool[] Delayed = new bool[2];
        public string Colour;

        public void Init()
        {
            for (int i = 0; i < 4; i++)
                PulsingSequence[i] = new bool[12];
            for (int i = 0; i < 12; i++)
                RotationSequence[i] = false;
            for (int i = 0; i < 2; i++)
            {
                OnStartingAngle[i] = 0;
                OnAngle[i] = 0;
                OffStartingAngle[i] = 0;
                OffAngle[i] = 0;
                Delayed[i] = false;
            }
            Colour = "";
        }
    }

    public List<Section> Sections = new List<Section>();
    private int currentSectionIndex = 0;

    // Pulsing sequence index for each light type
    private int scenePulsingStep = 0;
    private int ceilingPulsingStep = 0;
    private int secondPulsingStep = 0;

    void Start()
    {


        PopulateSectionsFromLandingMenu();
        LoadSectionUI(0);

        // Section navigation with bounds check
        nextSectionButton.onClick.AddListener(() => {
            if (currentSectionIndex < Sections.Count - 1)
            {
                SaveSectionUI(currentSectionIndex);
                currentSectionIndex++;
                ResetPulsingSteps();
                LoadSectionUI(currentSectionIndex);
            }
            else if (currentSectionIndex == Sections.Count - 1)
            {
                // Last section, show completion image and hide editing image
                if (completionImage != null) completionImage.gameObject.SetActive(true);
                if (editingImage != null) editingImage.gameObject.SetActive(false);
            }
        });
        prevSectionButton.onClick.AddListener(() => {
            if (currentSectionIndex > 0)
            {
                SaveSectionUI(currentSectionIndex);
                currentSectionIndex--;
                ResetPulsingSteps();
                LoadSectionUI(currentSectionIndex);
            }
        });

        // Pulsing sequence navigation
        scenePulsingPrevButton.onClick.AddListener(() => { ChangeScenePulsingStep(-1); });
        scenePulsingNextButton.onClick.AddListener(() => { ChangeScenePulsingStep(1); });
        ceilingPulsingPrevButton.onClick.AddListener(() => { ChangeCeilingPulsingStep(-1); });
        ceilingPulsingNextButton.onClick.AddListener(() => { ChangeCeilingPulsingStep(1); });
        secondPulsingPrevButton.onClick.AddListener(() => { ChangeSecondPulsingStep(-1); });
        secondPulsingNextButton.onClick.AddListener(() => { ChangeSecondPulsingStep(1); });
    }

    public void PopulateSectionsFromLandingMenu()
    {
        Sections.Clear();
        if (landingMenu == null) return;
        foreach (var item in landingMenu.audioRangeList)
        {
            Section section = new Section();
            section.Name = item.name;
            section.SceneLights.Init();
            section.CeilingLights.Init();
            section.SecondFloorLights.Init();
            Sections.Add(section);
        }
        LoadSectionUI(0);
    }

    private void ResetPulsingSteps()
    {
        scenePulsingStep = 0;
        ceilingPulsingStep = 0;
        secondPulsingStep = 0;
    }

    // Loads the UI with the values from the current section
    private void LoadSectionUI(int index)
    {
        if (index < 0 || index >= Sections.Count) return;
        var section = Sections[index];
        sectionIndexText.text = $"Section {index + 1}/{Sections.Count}";

        // Scene Lights
        scenePulsingToggle.isOn = section.SceneLights.Pulsing;
        sceneRotationToggle.isOn = section.SceneLights.Rotation;
        for (int i = 0; i < 11; i++)
        {
            scenePulsingSequenceToggles[i].isOn = section.SceneLights.PulsingSequence[scenePulsingStep][i];
            sceneRotationSequenceToggles[i].isOn = section.SceneLights.RotationSequence[i];
        }
        for (int i = 0; i < 2; i++)
        {
            sceneOnStartingAngleInputs[i].text = section.SceneLights.OnStartingAngle[i].ToString();
            sceneOnAngleInputs[i].text = section.SceneLights.OnAngle[i].ToString();
            sceneOffStartingAngleInputs[i].text = section.SceneLights.OffStartingAngle[i].ToString();
            sceneOffAngleInputs[i].text = section.SceneLights.OffAngle[i].ToString();
            sceneDelayedToggles[i].isOn = section.SceneLights.Delayed[i];
        }
        sceneColourInput.text = section.SceneLights.Colour;
        scenePulsingIndexText.text = $"Step {scenePulsingStep + 1}/4";

        // Ceiling Lights
        ceilingPulsingToggle.isOn = section.CeilingLights.Pulsing;
        ceilingRotationToggle.isOn = section.CeilingLights.Rotation;
        for (int i = 0; i < 8; i++)
        {
            ceilingPulsingSequenceToggles[i].isOn = section.CeilingLights.PulsingSequence[ceilingPulsingStep][i];
            ceilingRotationSequenceToggles[i].isOn = section.CeilingLights.RotationSequence[i];
        }
        for (int i = 0; i < 2; i++)
        {
            ceilingOnStartingAngleInputs[i].text = section.CeilingLights.OnStartingAngle[i].ToString();
            ceilingOnAngleInputs[i].text = section.CeilingLights.OnAngle[i].ToString();
            ceilingOffStartingAngleInputs[i].text = section.CeilingLights.OffStartingAngle[i].ToString();
            ceilingOffAngleInputs[i].text = section.CeilingLights.OffAngle[i].ToString();
            ceilingDelayedToggles[i].isOn = section.CeilingLights.Delayed[i];
        }
        ceilingColourInput.text = section.CeilingLights.Colour;
        ceilingPulsingIndexText.text = $"Step {ceilingPulsingStep + 1}/4";

        // Second Floor Lights
        secondPulsingToggle.isOn = section.SecondFloorLights.Pulsing;
        secondRotationToggle.isOn = section.SecondFloorLights.Rotation;
        for (int i = 0; i < 12; i++)
        {
            secondPulsingSequenceToggles[i].isOn = section.SecondFloorLights.PulsingSequence[secondPulsingStep][i];
            secondRotationSequenceToggles[i].isOn = section.SecondFloorLights.RotationSequence[i];
        }
        for (int i = 0; i < 2; i++)
        {
            secondOnStartingAngleInputs[i].text = section.SecondFloorLights.OnStartingAngle[i].ToString();
            secondOnAngleInputs[i].text = section.SecondFloorLights.OnAngle[i].ToString();
            secondOffStartingAngleInputs[i].text = section.SecondFloorLights.OffStartingAngle[i].ToString();
            secondOffAngleInputs[i].text = section.SecondFloorLights.OffAngle[i].ToString();
            secondDelayedToggles[i].isOn = section.SecondFloorLights.Delayed[i];
        }
        secondColourInput.text = section.SecondFloorLights.Colour;
        secondPulsingIndexText.text = $"Step {secondPulsingStep + 1}/4";
    }

    // Save UI values back to the section
    private void SaveSectionUI(int index)
    {
        if (index < 0 || index >= Sections.Count) return;
        var section = Sections[index];

        // Scene Lights
        section.SceneLights.Pulsing = scenePulsingToggle.isOn;
        section.SceneLights.Rotation = sceneRotationToggle.isOn;
        for (int i = 0; i < 11; i++)
        {
            section.SceneLights.PulsingSequence[scenePulsingStep][i] = scenePulsingSequenceToggles[i].isOn;
            section.SceneLights.RotationSequence[i] = sceneRotationSequenceToggles[i].isOn;
        }
        for (int i = 0; i < 2; i++)
        {
            float.TryParse(sceneOnStartingAngleInputs[i].text, out section.SceneLights.OnStartingAngle[i]);
            float.TryParse(sceneOnAngleInputs[i].text, out section.SceneLights.OnAngle[i]);
            float.TryParse(sceneOffStartingAngleInputs[i].text, out section.SceneLights.OffStartingAngle[i]);
            float.TryParse(sceneOffAngleInputs[i].text, out section.SceneLights.OffAngle[i]);
            section.SceneLights.Delayed[i] = sceneDelayedToggles[i].isOn;
        }
        section.SceneLights.Colour = sceneColourInput.text;

        // Ceiling Lights
        section.CeilingLights.Pulsing = ceilingPulsingToggle.isOn;
        section.CeilingLights.Rotation = ceilingRotationToggle.isOn;
        for (int i = 0; i < 8; i++)
        {
            section.CeilingLights.PulsingSequence[ceilingPulsingStep][i] = ceilingPulsingSequenceToggles[i].isOn;
            section.CeilingLights.RotationSequence[i] = ceilingRotationSequenceToggles[i].isOn;
        }
        for (int i = 0; i < 2; i++)
        {
            float.TryParse(ceilingOnStartingAngleInputs[i].text, out section.CeilingLights.OnStartingAngle[i]);
            float.TryParse(ceilingOnAngleInputs[i].text, out section.CeilingLights.OnAngle[i]);
            float.TryParse(ceilingOffStartingAngleInputs[i].text, out section.CeilingLights.OffStartingAngle[i]);
            float.TryParse(ceilingOffAngleInputs[i].text, out section.CeilingLights.OffAngle[i]);
            section.CeilingLights.Delayed[i] = ceilingDelayedToggles[i].isOn;
        }
        section.CeilingLights.Colour = ceilingColourInput.text;

        // Second Floor Lights
        section.SecondFloorLights.Pulsing = secondPulsingToggle.isOn;
        section.SecondFloorLights.Rotation = secondRotationToggle.isOn;
        for (int i = 0; i < 12; i++)
        {
            section.SecondFloorLights.PulsingSequence[secondPulsingStep][i] = secondPulsingSequenceToggles[i].isOn;
            section.SecondFloorLights.RotationSequence[i] = secondRotationSequenceToggles[i].isOn;
        }
        for (int i = 0; i < 2; i++)
        {
            float.TryParse(secondOnStartingAngleInputs[i].text, out section.SecondFloorLights.OnStartingAngle[i]);
            float.TryParse(secondOnAngleInputs[i].text, out section.SecondFloorLights.OnAngle[i]);
            float.TryParse(secondOffStartingAngleInputs[i].text, out section.SecondFloorLights.OffStartingAngle[i]);
            float.TryParse(secondOffAngleInputs[i].text, out section.SecondFloorLights.OffAngle[i]);
            section.SecondFloorLights.Delayed[i] = secondDelayedToggles[i].isOn;
        }
        section.SecondFloorLights.Colour = secondColourInput.text;
    }

    // Save pulsing step UI before changing step
    private void SaveScenePulsingStepUI()
    {
        if (currentSectionIndex < 0 || currentSectionIndex >= Sections.Count) return;
        var section = Sections[currentSectionIndex];
        for (int i = 0; i < 11; i++)
            section.SceneLights.PulsingSequence[scenePulsingStep][i] = scenePulsingSequenceToggles[i].isOn;
    }
    private void SaveCeilingPulsingStepUI()
    {
        if (currentSectionIndex < 0 || currentSectionIndex >= Sections.Count) return;
        var section = Sections[currentSectionIndex];
        for (int i = 0; i < 8; i++)
            section.CeilingLights.PulsingSequence[ceilingPulsingStep][i] = ceilingPulsingSequenceToggles[i].isOn;
    }
    private void SaveSecondPulsingStepUI()
    {
        if (currentSectionIndex < 0 || currentSectionIndex >= Sections.Count) return;
        var section = Sections[currentSectionIndex];
        for (int i = 0; i < 12; i++)
            section.SecondFloorLights.PulsingSequence[secondPulsingStep][i] = secondPulsingSequenceToggles[i].isOn;
    }

    // Pulsing step navigation
    private void ChangeScenePulsingStep(int delta)
    {
        SaveScenePulsingStepUI();
        scenePulsingStep = Mathf.Clamp(scenePulsingStep + delta, 0, 3);
        LoadSectionUI(currentSectionIndex);
    }
    private void ChangeCeilingPulsingStep(int delta)
    {
        SaveCeilingPulsingStepUI();
        ceilingPulsingStep = Mathf.Clamp(ceilingPulsingStep + delta, 0, 3);
        LoadSectionUI(currentSectionIndex);
    }
    private void ChangeSecondPulsingStep(int delta)
    {
        SaveSecondPulsingStepUI();
        secondPulsingStep = Mathf.Clamp(secondPulsingStep + delta, 0, 3);
        LoadSectionUI(currentSectionIndex);
    }
}