FILES=($(find ./output -name 'MorseL*.nupkg' | grep -v 'Test'))
for f in "${FILES[@]}"
do
    dotnet nuget push $f -s $1
done
