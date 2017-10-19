FILES=($(find ./output -name 'MorseL*.nupkg' | grep -v 'Test'))
for f in "${FILES[@]}"
do
    nuget add $f -Source $1
done