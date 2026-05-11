import { defineCollection, z } from 'astro:content';
import { glob } from 'astro/loaders';

const blogCollection = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/blog' }),
  schema: z.object({
    title: z.string(),
    description: z.string(),
    pubDate: z.coerce.date(),
    author: z.string().default('The Green Dots Team'),
    ogImage: z.string().optional(),
    keywords: z.array(z.string()),
    category: z.string(),
    featured: z.boolean().default(false),
  }),
});

export const collections = {
  'blog': blogCollection,
};
