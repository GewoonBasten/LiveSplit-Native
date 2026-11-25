// Port of LiveSplit.Core UI/IInvalidator.cs @ 93a11b85 (MIT).
using System.Drawing.Drawing2D;

namespace LiveSplit.UI;

public interface IInvalidator
{
    Matrix Transform { get; set; }
    void Invalidate(float x, float y, float width, float height);
}
