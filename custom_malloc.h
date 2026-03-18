#ifndef CUSTOM_MALLOC_H
#define CUSTOM_MALLOC_H

#include <stddef.h>

void *custom_malloc(size_t size);
void custom_free(void *ptr);
void *custom_calloc(size_t nmemb, size_t size);
void *custom_realloc(void *ptr, size_t size);

typedef void (*block_visitor_t)(size_t size, int is_free, void *user_data);
void custom_malloc_visit_blocks(block_visitor_t visitor, void *user_data);

void custom_malloc_reset(void);

#endif /* CUSTOM_MALLOC_H */
