using SPECTRA.Core.Streaming;

namespace SPECTRA.App.Platforms;

public interface IVideoSurface
{
    Task AttachAsync(ScrcpySession session, CancellationToken cancellationToken);
    void Detach();
}
