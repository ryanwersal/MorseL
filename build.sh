unset temp
unset tmp

FILES=($(find . -name '*.csproj' | grep -v 'Test'))
for f in "${FILES[@]}"
do
    dotnet restore $f
done

dotnet publish