namespace FantasyF1.Models.GridRival;

public class GrListResponse
{
    public List<GrPreviousElement> previous_elements { get; set; }
    public Dictionary<int, GrFpByElement> fp_by_element { get; set; }
}