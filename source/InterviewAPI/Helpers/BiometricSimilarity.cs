namespace InterviewAPI.Helpers;

public static class BiometricSimilarity
{
    public static double Cosine(float[] left, float[] right)
    {
        if (left.Length == 0 || left.Length != right.Length)
            return double.MinValue;

        double dot = 0;
        double leftNorm = 0;
        double rightNorm = 0;
        for (var i = 0; i < left.Length; i++)
        {
            dot += left[i] * right[i];
            leftNorm += left[i] * left[i];
            rightNorm += right[i] * right[i];
        }

        var denominator = Math.Sqrt(leftNorm) * Math.Sqrt(rightNorm);
        return denominator <= double.Epsilon ? double.MinValue : dot / denominator;
    }
}
