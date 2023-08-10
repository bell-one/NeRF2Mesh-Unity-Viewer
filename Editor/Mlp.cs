using Newtonsoft.Json;

public partial class Mlp {
    [JsonProperty("net.0.weight")]
    public double[][] _0Weights;

    [JsonProperty("net.1.weight")]
    public double[][] _1Weights;

    [JsonProperty("bound")]
    public double bound;

    [JsonProperty("cascade")]
    public int cascade;
}