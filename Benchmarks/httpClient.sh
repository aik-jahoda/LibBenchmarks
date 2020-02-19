BENCHMARK_DRIVER=~/workspace/Benchmarks/src/BenchmarksDriver2
LIB_BENCHMARKS=~/workspace/LibBenchmarks


cd $BENCHMARK_DRIVER
dotnet run -- \
    --config $LIB_BENCHMARKS/Benchmarks/httpClient.yml \
    --scenario httpclient \
\
    `#--kestrel.endpoints http://localhost:5002` \
    --kestrel.endpoints http://asp-perf-win:5001 \
    --kestrel.options.displayOutput true \
    --kestrel.options.displayBuild true \
\
    `#--client.endpoints http://localhost:5001` \
    --client.endpoints http://asp-perf-load:5001 \
    --client.options.displayOutput true \
    --client.options.displayBuild true \
    #--client.aspNetCoreVersion 5.0.0-alpha1.19380.3 \
    #--client.runtimeVersion 5.0.0-alpha1.19472.2

cd -