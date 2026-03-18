CC      = gcc
CFLAGS  = -Wall -Wextra -Wpedantic -g -O0 -fPIC

LIB     = libcustom_malloc.so  # .so means shared object (dynamic library on Unix/Linux)
SRC     = custom_malloc.c

.PHONY: all run clean

all: $(LIB)

$(LIB): $(SRC) custom_malloc.h
	$(CC) $(CFLAGS) -shared -o $(LIB) $(SRC)

run: $(LIB)
	dotnet test csharp_test.csproj --nologo -v m

clean:
	rm -f $(LIB)
	dotnet clean --nologo -v q
