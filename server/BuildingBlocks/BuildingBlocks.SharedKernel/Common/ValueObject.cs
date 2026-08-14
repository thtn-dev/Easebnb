using Dayline.BuildingBlocks.Utils.Extensions;

namespace BuildingBlocks.SharedKernel.Common;

[Serializable]
[Obsolete("ValueObject is deprecated. Consider using record types for value semantics instead.")]
public abstract class ValueObject : IEquatable<ValueObject>
{
    public bool Equals(ValueObject? other)
    {
        if (ReferenceEquals(this, other)) return true;
        return other != null && GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    protected static bool EqualOperator(ValueObject? left, ValueObject? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    protected static bool NotEqualOperator(ValueObject? left, ValueObject? right)
    {
        return !EqualOperator(left, right);
    }

    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj == null || obj.GetType() != GetType()) return false;

        return Equals((ValueObject)obj);
    }

    public override int GetHashCode()
    {
        return GetEqualityComponents().CombineHashCodes();
    }

    public static bool operator ==(ValueObject? left, ValueObject? right)
    {
        return EqualOperator(left, right);
    }

    public static bool operator !=(ValueObject? left, ValueObject? right)
    {
        return NotEqualOperator(left, right);
    }

    public virtual ValueObject GetCopy()
    {
        return (ValueObject)MemberwiseClone();
    }
}