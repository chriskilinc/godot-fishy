using Godot;
using System.Threading.Tasks;

public partial class SoundManager : Node
{
    [Signal]
    public delegate void MuteChangedEventHandler(bool muted);

    public bool IsMuted { get; private set; } = false;

    [Export]
    public bool StartMuted = true;

    [Export]
    public AudioStream[] MusicStreams = [];

    [Export]
    public AudioStream[] AmbientStreams = [];

    [Export]
    public AudioStream[] EatSfxOptions = [];

    [Export]
    public AudioStream[] GrowSfxOptions = [];

    [Export]
    public AudioStream[] DeathSfxOptions = [];

    [Export]
    public AudioStream[] FleeSfxOptions = [];

    [Export]
    public AudioStream[] ComboSfxOptions = [];

    [Export(PropertyHint.Range, "-40,12,0.1")]
    public float MusicVolumeDb = -10.0f;

    [Export(PropertyHint.Range, "-40,12,0.1")]
    public float AmbientVolumeDb = -16.0f;

    [Export(PropertyHint.Range, "-40,12,0.1")]
    public float SfxVolumeDb = -8.0f;

    [Export(PropertyHint.Range, "-40,12,0.1")]
    public float FleeVolumeDb = -14.0f;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float FleeMinIntervalSeconds = 1.2f;

    [Export(PropertyHint.Range, "1,20,1")]
    public int FleeMaxPerWindow = 3;

    [Export(PropertyHint.Range, "0,30,0.1")]
    public float FleeWindowSeconds = 6.0f;

    [Export(PropertyHint.Range, "0,30,0.1")]
    public float FleeStartupDelaySeconds = 4.0f;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float MusicFadeSeconds = 1.5f;

    [Export(PropertyHint.Range, "0,60,0.1")]
    public float MusicWaitMinSeconds = 3.0f;

    [Export(PropertyHint.Range, "0,60,0.1")]
    public float MusicWaitMaxSeconds = 7.0f;

    [Export(PropertyHint.Range, "1,16,1")]
    public int SfxVoiceCount = 6;

    [Export]
    public string MusicBus = "Music";

    [Export]
    public string AmbientBus = "Ambient";

    [Export]
    public string SfxBus = "SFX";

    private AudioStreamPlayer _musicPlayer;
    private AudioStreamPlayer _ambientPlayer;
    private AudioStreamPlayer[] _sfxPlayers = [];
    private int _nextSfxPlayerIndex = 0;
    private int _nextMusicIndex = 0;
    private int _musicSequenceVersion = 0;
    private AudioStream _ambientLoopStream;
    private readonly RandomNumberGenerator _random = new();
    private ulong _fleeLastPlayedMs = 0;
    private int _fleePlayCount = 0;
    private ulong _fleeWindowStartMs = 0;
    private ulong _fleeStartMs = 0;
    private int _lastEatSfxIndex = -1;
    private int _lastGrowSfxIndex = -1;
    private int _lastDeathSfxIndex = -1;
    private int _lastFleeSfxIndex = -1;
    private int _lastComboSfxIndex = -1;

    public override void _Ready()
    {
        _random.Randomize();
        _fleeStartMs = Time.GetTicksMsec();
        _musicPlayer = EnsurePlayer("MusicPlayer", MusicBus, MusicVolumeDb);
        _ambientPlayer = EnsurePlayer("AmbientPlayer", AmbientBus, AmbientVolumeDb);
        _ambientPlayer.Finished += OnAmbientFinished;
        EnsureSfxPlayers();
        SetMuted(StartMuted);
    }

    public void StartLoops()
    {
        StartAmbientLoop();
        StartMusicSequence();
    }

    public void PlayEat()
    {
        PlayOneShot(PickRandomStream(EatSfxOptions, ref _lastEatSfxIndex));
    }

    public void PlayGrow()
    {
        PlayOneShot(PickRandomStream(GrowSfxOptions, ref _lastGrowSfxIndex));
    }

    public void PlayDeath()
    {
        PlayOneShot(PickRandomStream(DeathSfxOptions, ref _lastDeathSfxIndex));
    }

    public void PlayFlee()
    {
        var nowMs = Time.GetTicksMsec();

        if (nowMs - _fleeStartMs < (ulong)(Mathf.Max(0.0f, FleeStartupDelaySeconds) * 1000))
        {
            return;
        }

        if (nowMs - _fleeLastPlayedMs < (ulong)(Mathf.Max(0.0f, FleeMinIntervalSeconds) * 1000))
        {
            return;
        }

        if (nowMs - _fleeWindowStartMs > (ulong)(Mathf.Max(0.0f, FleeWindowSeconds) * 1000))
        {
            _fleeWindowStartMs = nowMs;
            _fleePlayCount = 0;
        }

        if (_fleePlayCount >= Mathf.Max(1, FleeMaxPerWindow))
        {
            return;
        }

        _fleeLastPlayedMs = nowMs;
        _fleePlayCount++;
        PlayOneShot(PickRandomStream(FleeSfxOptions, ref _lastFleeSfxIndex), FleeVolumeDb);
    }

    public void PlayCombo()
    {
        PlayOneShot(PickRandomStream(ComboSfxOptions, ref _lastComboSfxIndex));
    }

    public void ToggleMute()
    {
        SetMuted(!IsMuted);
    }

    public void SetMuted(bool muted)
    {
        if (IsMuted == muted)
        {
            return;
        }

        IsMuted = muted;
        int masterBusIndex = AudioServer.GetBusIndex("Master");
        if (masterBusIndex >= 0)
        {
            AudioServer.SetBusMute(masterBusIndex, muted);
        }

        EmitSignal(SignalName.MuteChanged, muted);
    }

    private AudioStreamPlayer EnsurePlayer(string nodeName, string requestedBus, float volumeDb)
    {
        var player = GetNodeOrNull<AudioStreamPlayer>(nodeName);
        if (player != null)
        {
            player.Bus = ResolveBus(requestedBus);
            player.VolumeDb = volumeDb;
            return player;
        }

        player = new AudioStreamPlayer
        {
            Name = nodeName,
            Bus = ResolveBus(requestedBus),
            VolumeDb = volumeDb
        };

        AddChild(player);
        return player;
    }

    private void StartAmbientLoop()
    {
        if (_ambientPlayer == null)
        {
            return;
        }

        _ambientLoopStream ??= PickFirstValidStream(AmbientStreams);
        if (_ambientLoopStream == null)
        {
            return;
        }

        _ambientPlayer.VolumeDb = AmbientVolumeDb;
        _ambientPlayer.Stream = _ambientLoopStream;
        if (!_ambientPlayer.Playing)
        {
            _ambientPlayer.Play();
        }
    }

    private void OnAmbientFinished()
    {
        StartAmbientLoop();
    }

    private void StartMusicSequence()
    {
        _musicSequenceVersion++;
        _musicPlayer.Stop();
        _ = RunMusicSequenceAsync(_musicSequenceVersion);
    }

    private async Task RunMusicSequenceAsync(int version)
    {
        if (_musicPlayer == null || MusicStreams.Length == 0)
        {
            return;
        }

        while (version == _musicSequenceVersion && IsInsideTree())
        {
            var nextStream = PickNextMusicStream();
            if (nextStream == null)
            {
                return;
            }

            _musicPlayer.Stream = nextStream;
            _musicPlayer.VolumeDb = -80.0f;
            _musicPlayer.Play();

            await FadeVolumeAsync(_musicPlayer, MusicVolumeDb, MusicFadeSeconds, version);
            if (version != _musicSequenceVersion || !_musicPlayer.Playing)
            {
                return;
            }

            await ToSignal(_musicPlayer, AudioStreamPlayer.SignalName.Finished);
            if (version != _musicSequenceVersion)
            {
                return;
            }

            var waitSeconds = Mathf.Max(0.0f, _random.RandfRange(
                Mathf.Min(MusicWaitMinSeconds, MusicWaitMaxSeconds),
                Mathf.Max(MusicWaitMinSeconds, MusicWaitMaxSeconds)));

            if (waitSeconds > 0.0f)
            {
                var timer = GetTree().CreateTimer(waitSeconds);
                await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
            }
        }
    }

    private async Task FadeVolumeAsync(AudioStreamPlayer player, float targetVolumeDb, float fadeSeconds, int version)
    {
        if (player == null || version != _musicSequenceVersion)
        {
            return;
        }

        if (fadeSeconds <= 0.0f)
        {
            player.VolumeDb = targetVolumeDb;
            return;
        }

        var tween = CreateTween();
        tween.TweenProperty(player, "volume_db", targetVolumeDb, fadeSeconds);
        await ToSignal(tween, Tween.SignalName.Finished);
    }

    private void PlayOneShot(AudioStream stream, float? volumeDbOverride = null)
    {
        if (stream == null || _sfxPlayers.Length == 0)
        {
            return;
        }

        var player = _sfxPlayers[_nextSfxPlayerIndex];
        _nextSfxPlayerIndex = (_nextSfxPlayerIndex + 1) % _sfxPlayers.Length;

        player.VolumeDb = volumeDbOverride ?? SfxVolumeDb;
        player.Stream = stream;
        player.Play();
    }

    private void EnsureSfxPlayers()
    {
        var voiceCount = Mathf.Max(1, SfxVoiceCount);
        _sfxPlayers = new AudioStreamPlayer[voiceCount];

        for (int i = 0; i < voiceCount; i++)
        {
            _sfxPlayers[i] = EnsurePlayer($"SfxPlayer{i}", SfxBus, SfxVolumeDb);
        }

        _nextSfxPlayerIndex = 0;
    }

    private AudioStream PickNextMusicStream()
    {
        if (MusicStreams.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < MusicStreams.Length; i++)
        {
            int idx = (_nextMusicIndex + i) % MusicStreams.Length;
            var stream = MusicStreams[idx];
            if (stream != null)
            {
                _nextMusicIndex = (idx + 1) % MusicStreams.Length;
                return stream;
            }
        }

        return null;
    }

    private AudioStream PickFirstValidStream(AudioStream[] options)
    {
        foreach (var stream in options)
        {
            if (stream != null)
            {
                return stream;
            }
        }

        return null;
    }

    private AudioStream PickRandomStream(AudioStream[] options, ref int lastPlayedIndex)
    {
        if (options == null || options.Length == 0)
        {
            lastPlayedIndex = -1;
            return null;
        }

        int validCount = 0;
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != null)
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            lastPlayedIndex = -1;
            return null;
        }

        bool canExcludeLast = validCount > 1
            && lastPlayedIndex >= 0
            && lastPlayedIndex < options.Length
            && options[lastPlayedIndex] != null;

        int candidateCount = canExcludeLast ? validCount - 1 : validCount;
        int selectedCandidate = _random.RandiRange(0, candidateCount - 1);

        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] == null)
            {
                continue;
            }

            if (canExcludeLast && i == lastPlayedIndex)
            {
                continue;
            }

            if (selectedCandidate == 0)
            {
                lastPlayedIndex = i;
                return options[i];
            }

            selectedCandidate--;
        }

        // Fallback: return first valid stream and keep state consistent.
        for (int i = 0; i < options.Length; i++)
        {
            if (options[i] != null)
            {
                lastPlayedIndex = i;
                return options[i];
            }
        }

        lastPlayedIndex = -1;
        return null;
    }

    private string ResolveBus(string requestedBus)
    {
        return AudioServer.GetBusIndex(requestedBus) >= 0 ? requestedBus : "Master";
    }
}