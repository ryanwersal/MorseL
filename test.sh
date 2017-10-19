unset temp
unset tmp

FILES=($(find . -name '*.csproj' | grep -i 'Test'))
for f in "${FILES[@]}"
do
    dotnet test $f
done