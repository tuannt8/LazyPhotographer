namespace LazyPhotographer.Models
{
    public enum FlagStatus
    {
        Unflagged = 0,
        Flagged = 1,   // Picked / Selected
        Rejected = 2   // Rejected
    }

    public enum FilterSelection
    {
        All = 0,
        FlaggedOnly = 1,
        UnflaggedOnly = 2,
        RejectedOnly = 3
    }

    public enum ViewMode
    {
        Grid = 0,
        LoupeWithFilmstrip = 1
    }

    public enum ExportFileFormat
    {
        Jpeg = 0,
        Png = 1,
        Tiff16 = 2
    }
}
