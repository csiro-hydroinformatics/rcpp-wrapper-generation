% Generated by roxygen2: do not edit by hand
% Please edit documentation in R/sd-codegen.r
\name{generate_rpp_rcpp_glue}
\alias{generate_rpp_rcpp_glue}
\title{generate_rpp_rcpp_glue}
\usage{
generate_rpp_rcpp_glue(
  rppSrcPath,
  prepend_header = default_rpp_cppgen_prepend()
)
}
\arguments{
\item{rppSrcPath}{the root of rpp src codebase.}

\item{prepend_header}{the text to prepend to the generated C++ code, e.g. includes and helper data marshalling functions.}
}
\description{
generate C++ wrappers for the rpp R package, derived from the SWIFT C API.
}
