    ///
    public enum ProtocolVersion
    {
        ///
        Http10,
        ///
        Http11,
        ///
        Http20,

#if NETCOREAPP5_0
        //
        Http30,
#endif
    }