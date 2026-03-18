CC      = gcc
CFLAGS  = -Wall -Wextra -Wpedantic -g -O0 -fPIC

LIB     = libcustom_malloc.so
SRC     = custom_malloc.c

.PHONY: all run clean

all: $(LIB)

$(LIB): $(SRC) custom_malloc.h
	$(CC) $(CFLAGS) -shared -o $@ $(SRC)

run: $(LIB)
	dotnet test csharp_test.csproj --nologo -v m

clean:
	rm -f $(LIB)
	dotnet clean --nologo -v q
