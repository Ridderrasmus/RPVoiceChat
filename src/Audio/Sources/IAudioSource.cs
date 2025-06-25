public interface IAudioSource
{
    void Play();
    void Update(float deltaTime);
    bool IsFinished();
}