#include "custom_malloc.h"
#include <pthread.h> /* pthread_mutex_lock     */
#include <stdbool.h> /* bool, true, false      */
#include <stdint.h>  /* SIZE_MAX               */
#include <string.h>  /* memset, memcpy         */
#include <unistd.h>  /* sbrk                   */

static pthread_mutex_t heap_mutex = PTHREAD_MUTEX_INITIALIZER;

#define ALIGN (sizeof(void *))
#define ALIGN_UP(n) (((n) + ALIGN - 1) & ~(ALIGN - 1))

typedef struct block {
  size_t size;
  bool free;
  struct block *next;
} block_t;

#define HEADER_SIZE ALIGN_UP(sizeof(block_t))

static block_t *heap_head = NULL;
static void *heap_start = NULL;

static block_t *find_free(size_t size) {
  for (block_t *b = heap_head; b != NULL; b = b->next) {
    if (b->free && b->size >= size) {
      return b;
    }
  }
  return NULL;
}

/* Ask the OS for more memory and append a new block to the list. */
static block_t *extend_heap(size_t size) {
  block_t *b = sbrk(0); /* current break */
  if (!heap_start) {
    heap_start = b;
  }
  void *ret = sbrk((intptr_t)(HEADER_SIZE + size));
  if (ret == (void *)-1) {
    return NULL;
  }

  b->size = size;
  b->free = false;
  b->next = NULL;

  if (heap_head == NULL) {
    heap_head = b;
  } else {
    block_t *tail = heap_head;
    while (tail->next) {
      tail = tail->next;
    }
    tail->next = b;
  }
  return b;
}

static void split_block(block_t *b, size_t size) {
  if (b->size < size + HEADER_SIZE + ALIGN) {
    return;
  }

  block_t *remainder = (block_t *)((char *)b + HEADER_SIZE + size);
  remainder->size = b->size - size - HEADER_SIZE;
  remainder->free = true;
  remainder->next = b->next;

  b->size = size;
  b->next = remainder;
}

static void coalesce(block_t *b) {
  while (b->next && b->next->free) {
    b->size += HEADER_SIZE + b->next->size;
    b->next = b->next->next;
  }
}

void *custom_malloc(size_t size) {
  pthread_mutex_lock(&heap_mutex);
  if (size == 0) {
    pthread_mutex_unlock(&heap_mutex);
    return NULL;
  }

  size = ALIGN_UP(size);

  block_t *b = find_free(size);
  if (b) {
    split_block(b, size);
    b->free = false;
  } else {
    b = extend_heap(size);
    if (!b) {
      pthread_mutex_unlock(&heap_mutex);
      return NULL;
    }
  }

  void *result = (char *)b + HEADER_SIZE;
  pthread_mutex_unlock(&heap_mutex);
  return result;
}

void custom_free(void *ptr) {
  pthread_mutex_lock(&heap_mutex);
  if (!ptr) {
    pthread_mutex_unlock(&heap_mutex);
    return;
  }

  block_t *b = (block_t *)((char *)ptr - HEADER_SIZE);
  b->free = true;

  /* Walk from head so we can coalesce upward in a single pass. */
  for (block_t *cur = heap_head; cur != NULL; cur = cur->next) {
    if (cur->free) {
      coalesce(cur);
    }
  }

  /* Shrink the heap if the last block is free. */
  block_t *prev = NULL, *tail = heap_head;
  while (tail && tail->next) {
    prev = tail;
    tail = tail->next;
  }
  if (tail && tail->free &&
      sbrk(-(intptr_t)(HEADER_SIZE + tail->size)) != (void *)-1) {
    if (prev) {
      prev->next = NULL;
    } else {
      heap_head = NULL;
    }
  }
  pthread_mutex_unlock(&heap_mutex);
}

void *custom_calloc(size_t nmemb, size_t size) {
  /* Guard against multiplication overflow. */
  if (nmemb != 0 && size > SIZE_MAX / nmemb) {
    return NULL;
  }

  size_t total = nmemb * size;
  void *ptr = custom_malloc(total);
  if (ptr) {
    memset(ptr, 0, total);
  }
  return ptr;
}

void *custom_realloc(void *ptr, size_t size) {
  if (!ptr) {
    return custom_malloc(size);
  }

  if (size == 0) {
    custom_free(ptr);
    return NULL;
  }

  block_t *b = (block_t *)((char *)ptr - HEADER_SIZE);
  size_t aligned = ALIGN_UP(size);

  /* The current block is already large enough — return it as-is. */
  if (b->size >= aligned) {
    return ptr;
  }

  /* Extend in-place: absorb the next block if it is free and big enough. */
  if (b->next && b->next->free &&
      b->size + HEADER_SIZE + b->next->size >= aligned) {
    b->size += HEADER_SIZE + b->next->size;
    b->next = b->next->next;
    /* Split off any unused tail so it stays available. */
    split_block(b, aligned);
    return ptr;
  }

  /* Last resort: allocate elsewhere, copy, free the old block. */
  void *new_ptr = custom_malloc(size);
  if (!new_ptr) {
    return NULL;
  }

  memcpy(new_ptr, ptr, b->size); /* copy only the valid payload bytes  */
  custom_free(ptr);
  return new_ptr;
}

void custom_malloc_visit_blocks(block_visitor_t visitor, void *user_data) {
  pthread_mutex_lock(&heap_mutex);
  for (block_t *b = heap_head; b != NULL; b = b->next) {
    visitor(b->size, b->free ? 1 : 0, user_data);
  }
  pthread_mutex_unlock(&heap_mutex);
}

void custom_malloc_reset(void) {
  pthread_mutex_lock(&heap_mutex);
  if (heap_start) {
    void *cur = sbrk(0);
    if (cur != heap_start) {
      sbrk((intptr_t)heap_start - (intptr_t)cur);
    }
  }
  heap_head = NULL;
  heap_start = NULL;
  pthread_mutex_unlock(&heap_mutex);
}
